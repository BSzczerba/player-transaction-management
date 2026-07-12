using Application.DTOs;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="Application.Services.Implementations.TransactionService.CreateWithdrawalAsync"/>.
/// </summary>
public class CreateWithdrawalTests : TransactionServiceTestBase
{
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
}
