using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="Application.Services.Implementations.TransactionService.CreateDepositAsync"/>.
/// </summary>
public class CreateDepositTests : TransactionServiceTestBase
{
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
        player.Balance.Should().Be(initialBalance + 50m, "the player entity itself must be updated, not just the returned DTO");
        _players.Verify(r => r.Update(player), Times.Once);
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
        var balanceBefore = player.Balance;

        // Act
        var result = await _sut.CreateDepositAsync(PlayerId, dto, null);

        // Assert
        result.IsFlagged.Should().BeTrue();
        result.FlagReason.Should().Contain("velocity");
        result.Status.Should().Be("Pending"); // NOT auto-approved despite amount < 100
        player.Balance.Should().Be(balanceBefore, "balance must not be updated for a flagged pending transaction");
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
}
