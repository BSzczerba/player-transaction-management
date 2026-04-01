using Application.DTOs;
using Application.Models;
using Application.Repositories.Interfaces;
using Application.Services.Implementations;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="TransactionService"/>.
/// All external dependencies are mocked so tests are fast and fully isolated.
/// </summary>
public class TransactionServiceTests
{
    // ─── Shared mocks ────────────────────────────────────────────────────────

    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IPaymentMethodRepository> _paymentMethods = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly Mock<INotificationService> _notifications = new();
    private readonly Mock<IPaymentGatewayService> _gateway = new();
    private readonly IMapper _mapper;

    private readonly TransactionService _sut;

    // ─── Shared test data ────────────────────────────────────────────────────

    private static readonly Guid PlayerId = Guid.NewGuid();
    private static readonly Guid OperatorId = Guid.NewGuid();
    private static readonly Guid PaymentMethodId = Guid.NewGuid();

    private static readonly Player ActiveKycPlayer = new()
    {
        Id = PlayerId,
        Username = "testplayer",
        Email = "player@test.com",
        Status = AccountStatus.Active,
        KycVerified = true,
        Balance = 1_000m,
        DailyDepositLimit = 10_000m,
        DailyWithdrawalLimit = 5_000m,
        RowVersion = [1]
    };

    private static readonly PaymentMethod CreditCardMethod = new()
    {
        Id = PaymentMethodId,
        Name = "Visa",
        Type = PaymentMethodType.CreditCard,
        IsActive = true,
        MinAmount = 10m,
        MaxAmount = 5_000m,
        FeePercentage = 1.5m,
        FixedFee = 0m,
        ProcessingTimeMinutes = 1
    };

    public TransactionServiceTests()
    {
        // Wire up UoW to return individual repo mocks
        _uow.Setup(u => u.Players).Returns(_players.Object);
        _uow.Setup(u => u.Transactions).Returns(_transactions.Object);
        _uow.Setup(u => u.PaymentMethods).Returns(_paymentMethods.Object);
        _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Audit doesn't call SaveChanges — match full optional-parameter signature
        _audit.Setup(a => a.LogAsync(
            It.IsAny<Guid?>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // Notifications return a DTO (CreateAsync is Task<NotificationDto>)
        _notifications.Setup(n => n.CreateAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<Guid?>(),
            It.IsAny<CancellationToken>())).ReturnsAsync(new Application.DTOs.NotificationDto());

        // AutoMapper — real profile so DTO mapping is tested accurately
        var config = new MapperConfiguration(cfg => cfg.AddProfile<Application.Mappings.MappingProfile>());
        _mapper = config.CreateMapper();

        _sut = new TransactionService(
            _uow.Object, _mapper, NullLogger<TransactionService>.Instance,
            _audit.Object, _notifications.Object, _gateway.Object);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE DEPOSIT — happy path
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDepositAsync_BelowThreshold_NoAmlFlags_AutoApproves()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };
        var initialBalance = player.Balance;

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, "127.0.0.1");

        // Assert
        result.Status.Should().Be("Completed");
        result.Amount.Should().Be(50m);
        result.IsFlagged.Should().BeFalse();
        result.BalanceAfter.Should().Be(initialBalance + 50m);
    }

    [Fact]
    public async Task CreateDepositAsync_AtOrAboveThreshold_RequiresManualApproval()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 100m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateDepositAsync_LargeUnflaggedAmount_RequiresManualApproval()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, balance: 50_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Bank", Type = PaymentMethodType.BankTransfer,
            IsActive = true, MinAmount = 0m, MaxAmount = 100_000m
        });
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 500m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.Status.Should().Be("Pending");
        result.IsFlagged.Should().BeFalse();
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE DEPOSIT — AML detection
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDepositAsync_FiveOrMoreTransactionsIn24h_FlagsForAml()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);

        // 5 completed transactions in the last 24 h → velocity trigger
        var recentTxns = Enumerable.Range(0, 5)
            .Select(_ => new Transaction
            {
                Amount = 20m,
                Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed,
                PlayerId = PlayerId
            })
            .ToList();

        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);

        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateDepositDto { Amount = 20m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeTrue();
        result.FlagReason.Should().Contain("velocity");
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateDepositAsync_AmountExceedsSingleTransactionLimit_FlagsForAml()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, balance: 100_000m, dailyDepositLimit: 100_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Bank", Type = PaymentMethodType.BankTransfer,
            IsActive = true, MinAmount = 0m, MaxAmount = 50_000m
        });
        SetupEmptyTransactionHistory();
        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateDepositDto { Amount = 15_000m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeTrue();
        result.FlagReason.Should().Contain("threshold");
    }

    [Fact]
    public async Task CreateDepositAsync_DailyVolumeExceedsLimit_FlagsForAml()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, balance: 100_000m, dailyDepositLimit: 100_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Bank", Type = PaymentMethodType.BankTransfer,
            IsActive = true, MinAmount = 0m, MaxAmount = 25_000m
        });

        // 19 000 deposited in the last 24 h → adding 5 000 pushes total to 24 000 > 20 000 threshold
        var recentTxns = new List<Transaction>
        {
            new() { Amount = 19_000m, Type = TransactionType.Deposit, Status = TransactionStatus.Completed, PlayerId = PlayerId }
        };
        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateDepositDto { Amount = 5_000m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeTrue();
        result.FlagReason.Should().Contain("volume");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE DEPOSIT — validation failures
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDepositAsync_SuspendedAccount_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, status: AccountStatus.Suspended);
        SetupPlayerRepo(player);

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task CreateDepositAsync_DailyLimitExceeded_Throws()
    {
        // Arrange — player already deposited 9 900, limit is 10 000
        var player = ClonePlayer(ActiveKycPlayer, dailyDepositLimit: 10_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);

        var existingDeposits = new List<Transaction>
        {
            new() { Amount = 9_900m, Type = TransactionType.Deposit, Status = TransactionStatus.Completed, PlayerId = PlayerId }
        };
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingDeposits);
        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateDepositDto { Amount = 200m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*limit*");
    }

    [Fact]
    public async Task CreateDepositAsync_AmountBelowPaymentMethodMin_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod); // min = 10
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 5m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*between*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE WITHDRAWAL — validation failures
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateWithdrawalAsync_KycNotVerified_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, kycVerified: false);
        SetupPlayerRepo(player);

        var dto = new CreateWithdrawalDto { Amount = 100m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*KYC*");
    }

    [Fact]
    public async Task CreateWithdrawalAsync_InsufficientBalance_Throws()
    {
        // Arrange — balance is 50, trying to withdraw 200
        var player = ClonePlayer(ActiveKycPlayer, balance: 50m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);
        SetupEmptyTransactionHistory();

        var dto = new CreateWithdrawalDto { Amount = 200m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*balance*");
    }

    [Fact]
    public async Task CreateWithdrawalAsync_ValidRequest_CreatesPendingTransaction()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);
        SetupEmptyTransactionHistory();

        var dto = new CreateWithdrawalDto { Amount = 200m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        result.Status.Should().Be("Pending");
        result.Amount.Should().Be(200m);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // APPROVE
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApproveAsync_GatewaySuccess_CompletesTransactionAndUpdatesBalance()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        var transaction = MakePendingDeposit(200m, player.Id);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));
        _players.Setup(r => r.Update(It.IsAny<Player>()));

        _gateway.Setup(g => g.ProcessPaymentAsync(transaction.Id, 200m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Succeeded("PAY-ABCDEF123456"));

        // Act
        var result = await _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        result.Status.Should().Be("Completed");
        result.PaymentGatewayReference.Should().Be("PAY-ABCDEF123456");
        player.Balance.Should().Be(ActiveKycPlayer.Balance + 200m);
    }

    [Fact]
    public async Task ApproveAsync_GatewayFailure_MarksTransactionAsFailed_BalanceUnchanged()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        var originalBalance = player.Balance;
        var transaction = MakePendingDeposit(200m, player.Id);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));

        _gateway.Setup(g => g.ProcessPaymentAsync(transaction.Id, 200m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Failed("CARD_DECLINED", "The card was declined by the issuing bank."));

        // Act
        var result = await _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        result.Status.Should().Be("Failed");
        player.Balance.Should().Be(originalBalance, "balance must not change on gateway failure");
        _players.Verify(r => r.Update(It.IsAny<Player>()), Times.Never,
            "player entity must not be saved when gateway fails");
    }

    [Fact]
    public async Task ApproveAsync_TransactionNotPending_Throws()
    {
        // Arrange
        var transaction = MakePendingDeposit(200m, PlayerId);
        transaction.Status = TransactionStatus.Completed; // already processed

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var act = () => _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task ApproveAsync_OwnTransaction_Throws()
    {
        // Arrange — operator IS the player
        var transaction = MakePendingDeposit(200m, OperatorId);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var act = () => _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*own*");
    }

    [Fact]
    public async Task ApproveAsync_WithdrawalInsufficientBalance_Throws()
    {
        // Arrange — player balance dropped to 0 since withdrawal was created
        var player = ClonePlayer(ActiveKycPlayer, balance: 0m);
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PlayerId = PlayerId,
            Type = TransactionType.Withdrawal,
            Amount = 500m,
            Status = TransactionStatus.Pending,
            BalanceBefore = 500m,
            RowVersion = [1]
        };

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);

        _gateway.Setup(g => g.ProcessPaymentAsync(It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Succeeded("GTW-XYZ"));

        // Act
        var act = () => _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*balance*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // REJECT
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RejectAsync_ValidPendingTransaction_MarksAsRejected()
    {
        // Arrange
        var transaction = MakePendingDeposit(200m, PlayerId);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));

        // Act
        var result = await _sut.RejectAsync(transaction.Id, OperatorId, "Insufficient documentation");

        // Assert
        result.Status.Should().Be("Rejected");
        result.RejectionReason.Should().Be("Insufficient documentation");
    }

    [Fact]
    public async Task RejectAsync_AlreadyCompletedTransaction_Throws()
    {
        // Arrange
        var transaction = MakePendingDeposit(200m, PlayerId);
        transaction.Status = TransactionStatus.Completed;

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var act = () => _sut.RejectAsync(transaction.Id, OperatorId, "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // AML — compliance officer notification
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDepositAsync_AmlFlagged_NotifiesAllComplianceOfficers()
    {
        // Arrange
        // Raise limits so the amount doesn't trip the daily deposit guard before the AML check
        var player = ClonePlayer(ActiveKycPlayer, balance: 100_000m, dailyDepositLimit: 100_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Bank", Type = PaymentMethodType.BankTransfer,
            IsActive = true, MinAmount = 0m, MaxAmount = 50_000m
        });
        SetupEmptyTransactionHistory();

        var officer1 = new Player { Id = Guid.NewGuid(), Username = "compliance1", RowVersion = [1] };
        var officer2 = new Player { Id = Guid.NewGuid(), Username = "compliance2", RowVersion = [1] };
        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([officer1, officer2]);

        // Amount > 10 000 → AML flag
        var dto = new CreateDepositDto { Amount = 12_000m, PaymentMethodId = PaymentMethodId };

        // Act
        await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert — one notification per compliance officer
        _notifications.Verify(n => n.CreateAsync(
            officer1.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);

        _notifications.Verify(n => n.CreateAsync(
            officer2.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE DEPOSIT — player / payment-method guards
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDepositAsync_PlayerNotFound_Throws()
    {
        // Arrange
        _players.Setup(r => r.GetByIdAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Player?)null);

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateDepositAsync_PaymentMethodNotFound_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        _paymentMethods.Setup(r => r.GetByIdAsync(PaymentMethodId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((PaymentMethod?)null);

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task CreateDepositAsync_InactivePaymentMethod_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Visa", Type = PaymentMethodType.CreditCard,
            IsActive = false, MinAmount = 10m, MaxAmount = 5_000m
        });

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not active*");
    }

    [Fact]
    public async Task CreateDepositAsync_AmountAbovePaymentMethodMax_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod); // MaxAmount = 5 000

        var dto = new CreateDepositDto { Amount = 6_000m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*between*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE DEPOSIT — AML boundary & auto-approve interaction
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateDepositAsync_AmlFlagged_BelowAutoApproveThreshold_StillPending()
    {
        // AML flag must prevent auto-approve even when amount < 100
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);

        var recentTxns = Enumerable.Range(0, 5)
            .Select(_ => new Transaction
            {
                Amount = 20m, Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed, PlayerId = PlayerId
            })
            .ToList();

        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateDepositDto { Amount = 20m, PaymentMethodId = PaymentMethodId };
        var balanceBefore = player.Balance;

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeTrue();
        result.Status.Should().Be("Pending"); // NOT auto-approved despite amount < 100
        player.Balance.Should().Be(balanceBefore, "balance must not be updated for a flagged pending transaction");
    }

    [Fact]
    public async Task CreateDepositAsync_ExactlyAtAmlSingleAmountThreshold_DoesNotFlag()
    {
        // 10 000 exactly must NOT trigger AML — the guard is strictly >, not >=
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, balance: 100_000m, dailyDepositLimit: 100_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Bank", Type = PaymentMethodType.BankTransfer,
            IsActive = true, MinAmount = 0m, MaxAmount = 50_000m
        });
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 10_000m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeFalse();
        result.FlagReason.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task CreateDepositAsync_AutoApproved_UpdatesPlayerBalance()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        var balanceBefore = player.Balance;
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };

        // Act
        await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        player.Balance.Should().Be(balanceBefore + 50m);
        _players.Verify(r => r.Update(player), Times.Once);
    }

    [Fact]
    public async Task CreateDepositAsync_AuditLogCreated()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);
        SetupEmptyTransactionHistory();

        var dto = new CreateDepositDto { Amount = 50m, PaymentMethodId = PaymentMethodId };

        // Act
        await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        _audit.Verify(a => a.LogAsync(
            PlayerId, "CreateDeposit", "Transaction", It.IsAny<Guid?>(),
            It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE WITHDRAWAL — additional validation failures
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateWithdrawalAsync_SuspendedAccount_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, status: AccountStatus.Suspended);
        SetupPlayerRepo(player);

        var dto = new CreateWithdrawalDto { Amount = 100m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

    [Fact]
    public async Task CreateWithdrawalAsync_AmountAbovePaymentMethodMax_Throws()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod); // MaxAmount = 5 000

        var dto = new CreateWithdrawalDto { Amount = 6_000m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*between*");
    }

    [Fact]
    public async Task CreateWithdrawalAsync_DailyWithdrawalLimitExceeded_Throws()
    {
        // Arrange — player already withdrew 400 today, limit is 500
        var player = ClonePlayer(ActiveKycPlayer, dailyWithdrawalLimit: 500m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);

        var existingWithdrawals = new List<Transaction>
        {
            new()
            {
                Amount = 400m, Type = TransactionType.Withdrawal,
                Status = TransactionStatus.Completed, PlayerId = PlayerId
            }
        };
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existingWithdrawals);
        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateWithdrawalDto { Amount = 200m, PaymentMethodId = PaymentMethodId };

        // Act
        var act = () => _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*limit*");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CREATE WITHDRAWAL — AML detection
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task CreateWithdrawalAsync_AmlVelocity_FlagsAndStaysPending()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(CreditCardMethod);

        var recentTxns = Enumerable.Range(0, 5)
            .Select(_ => new Transaction
            {
                Amount = 20m, Type = TransactionType.Deposit,
                Status = TransactionStatus.Completed, PlayerId = PlayerId
            })
            .ToList();

        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(recentTxns);
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var dto = new CreateWithdrawalDto { Amount = 100m, PaymentMethodId = PaymentMethodId };

        // Act
        var result = await _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeTrue();
        result.Status.Should().Be("Pending");
    }

    [Fact]
    public async Task CreateWithdrawalAsync_AmlFlagged_NotifiesAllComplianceOfficers()
    {
        // Arrange — amount > 10 000 triggers AML single-amount check
        var player = ClonePlayer(ActiveKycPlayer, balance: 100_000m, dailyWithdrawalLimit: 100_000m);
        SetupPlayerRepo(player);
        SetupPaymentMethodRepo(new PaymentMethod
        {
            Id = PaymentMethodId, Name = "Bank", Type = PaymentMethodType.BankTransfer,
            IsActive = true, MinAmount = 0m, MaxAmount = 50_000m
        });
        SetupEmptyTransactionHistory();

        var officer = new Player { Id = Guid.NewGuid(), Username = "compliance1", RowVersion = [1] };
        _uow.Setup(u => u.Players.GetByRoleAsync(UserRole.ComplianceOfficer, It.IsAny<CancellationToken>()))
            .ReturnsAsync([officer]);

        var dto = new CreateWithdrawalDto { Amount = 12_000m, PaymentMethodId = PaymentMethodId };

        // Act
        await _sut.CreateWithdrawalAsync(PlayerId, dto, null);

        // Assert
        _notifications.Verify(n => n.CreateAsync(
            officer.Id, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // APPROVE — additional cases
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ApproveAsync_TransactionNotFound_Throws()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _transactions.Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        var act = () => _sut.ApproveAsync(nonExistentId, OperatorId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task ApproveAsync_WithdrawalGatewaySuccess_DecreasesBalance()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer, balance: 1_000m);
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PlayerId = PlayerId,
            Type = TransactionType.Withdrawal,
            Amount = 300m,
            Status = TransactionStatus.Pending,
            BalanceBefore = 1_000m,
            RowVersion = [1]
        };

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));
        _players.Setup(r => r.Update(It.IsAny<Player>()));

        _gateway.Setup(g => g.ProcessPaymentAsync(transaction.Id, 300m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Succeeded("PAY-WITHDRAW-001"));

        // Act
        var result = await _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        result.Status.Should().Be("Completed");
        player.Balance.Should().Be(700m, "balance must decrease by the withdrawal amount");
        _players.Verify(r => r.Update(player), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_GatewaySuccess_NotifiesPlayer()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        var transaction = MakePendingDeposit(200m, player.Id);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));
        _players.Setup(r => r.Update(It.IsAny<Player>()));

        _gateway.Setup(g => g.ProcessPaymentAsync(transaction.Id, 200m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Succeeded("PAY-NOTIFY-001"));

        // Act
        await _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        _notifications.Verify(n => n.CreateAsync(
            PlayerId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_GatewayFailure_NotifiesPlayer()
    {
        // Arrange
        var player = ClonePlayer(ActiveKycPlayer);
        var transaction = MakePendingDeposit(200m, player.Id);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));

        _gateway.Setup(g => g.ProcessPaymentAsync(transaction.Id, 200m, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Failed("DECLINED", "Card declined by issuer."));

        // Act
        await _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        _notifications.Verify(n => n.CreateAsync(
            PlayerId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ApproveAsync_PlayerNotFound_CallsRollback()
    {
        // Arrange — transaction exists but player lookup fails
        var transaction = MakePendingDeposit(200m, PlayerId);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _players.Setup(r => r.GetByIdAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Player?)null);

        // Act
        var act = () => _sut.ApproveAsync(transaction.Id, OperatorId, null);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // REJECT — additional cases
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RejectAsync_TransactionNotFound_Throws()
    {
        // Arrange
        var nonExistentId = Guid.NewGuid();
        _transactions.Setup(r => r.GetByIdAsync(nonExistentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction?)null);

        // Act
        var act = () => _sut.RejectAsync(nonExistentId, OperatorId, "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    [Fact]
    public async Task RejectAsync_ValidRejection_NotifiesPlayer()
    {
        // Arrange
        var transaction = MakePendingDeposit(200m, PlayerId);

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);
        _transactions.Setup(r => r.Update(It.IsAny<Transaction>()));

        // Act
        await _sut.RejectAsync(transaction.Id, OperatorId, "Insufficient documentation");

        // Assert
        _notifications.Verify(n => n.CreateAsync(
            PlayerId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // Helpers
    // ═════════════════════════════════════════════════════════════════════════

    private static Player ClonePlayer(
        Player source,
        decimal? balance = null,
        AccountStatus? status = null,
        bool? kycVerified = null,
        decimal? dailyDepositLimit = null,
        decimal? dailyWithdrawalLimit = null) =>
        new()
        {
            Id = source.Id,
            Username = source.Username,
            Email = source.Email,
            Status = status ?? source.Status,
            KycVerified = kycVerified ?? source.KycVerified,
            Balance = balance ?? source.Balance,
            DailyDepositLimit = dailyDepositLimit ?? source.DailyDepositLimit,
            DailyWithdrawalLimit = dailyWithdrawalLimit ?? source.DailyWithdrawalLimit,
            RowVersion = source.RowVersion
        };

    private static Transaction MakePendingDeposit(decimal amount, Guid playerId) =>
        new()
        {
            Id = Guid.NewGuid(),
            PlayerId = playerId,
            Type = TransactionType.Deposit,
            Amount = amount,
            Status = TransactionStatus.Pending,
            BalanceBefore = ActiveKycPlayer.Balance,
            Player = new Player { Id = playerId, Username = "player", RowVersion = [1] },
            RowVersion = [1]
        };

    private void SetupPlayerRepo(Player player)
    {
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _players.Setup(r => r.Update(It.IsAny<Player>()));
    }

    private void SetupPaymentMethodRepo(PaymentMethod pm)
    {
        _paymentMethods.Setup(r => r.GetByIdAsync(pm.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);
    }

    private void SetupEmptyTransactionHistory()
    {
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
    }
}
