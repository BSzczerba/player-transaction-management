using Application.DTOs;
using Application.Models;
using Application.Repositories.Interfaces;
using Application.Services.Implementations;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Shared mocks, test data, and helpers for <see cref="TransactionService"/> unit tests.
/// Split by operation into <c>CreateDepositTests</c>, <c>CreateWithdrawalTests</c>,
/// <c>ApproveTests</c>, and <c>RejectTests</c> — each gets a fresh instance of this base
/// (xUnit creates a new test-class instance per [Fact]), so no state leaks between tests.
/// </summary>
public abstract class TransactionServiceTestBase
{
    // ─── Shared mocks ────────────────────────────────────────────────────────

    protected readonly Mock<IUnitOfWork> _uow = new();
    protected readonly Mock<IPlayerRepository> _players = new();
    protected readonly Mock<ITransactionRepository> _transactions = new();
    protected readonly Mock<IPaymentMethodRepository> _paymentMethods = new();
    protected readonly Mock<IAuditService> _audit = new();
    protected readonly Mock<INotificationService> _notifications = new();
    protected readonly Mock<IPaymentGatewayService> _gateway = new();
    protected readonly IMapper _mapper;

    protected readonly TransactionService _sut;

    // ─── Shared test data ────────────────────────────────────────────────────

    protected static readonly Guid PlayerId = Guid.NewGuid();
    protected static readonly Guid OperatorId = Guid.NewGuid();
    protected static readonly Guid PaymentMethodId = Guid.NewGuid();

    protected static readonly Player ActiveKycPlayer = new()
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

    protected static readonly PaymentMethod CreditCardMethod = new()
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

    protected TransactionServiceTestBase()
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

        // Gateway — default success; individual tests override for failure scenarios
        _gateway.Setup(g => g.ProcessPaymentAsync(
            It.IsAny<Guid>(), It.IsAny<decimal>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PaymentGatewayResult.Succeeded("PAY-TEST-DEFAULT"));

        // AutoMapper — real profile so DTO mapping is tested accurately
        var config = new MapperConfiguration(cfg => cfg.AddProfile<Application.Mappings.MappingProfile>());
        _mapper = config.CreateMapper();

        _sut = new TransactionService(
            _uow.Object, _mapper, NullLogger<TransactionService>.Instance,
            _audit.Object, _notifications.Object, _gateway.Object);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    protected static Player ClonePlayer(
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

    protected static Transaction MakePendingDeposit(decimal amount, Guid playerId) =>
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

    protected void SetupPlayerRepo(Player player)
    {
        _players.Setup(r => r.GetByIdAsync(player.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _players.Setup(r => r.Update(It.IsAny<Player>()));
    }

    protected void SetupPaymentMethodRepo(PaymentMethod pm)
    {
        _paymentMethods.Setup(r => r.GetByIdAsync(pm.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(pm);
    }

    protected void SetupEmptyTransactionHistory()
    {
        _transactions.Setup(r => r.GetTodaysTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _transactions.Setup(r => r.GetLast24HoursTransactionsByPlayerAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _transactions.Setup(r => r.AddAsync(It.IsAny<Transaction>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Transaction t, CancellationToken _) => t);
    }
}
