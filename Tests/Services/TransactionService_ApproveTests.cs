using Application.Models;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="Application.Services.Implementations.TransactionService.ApproveAsync"/>.
/// </summary>
public class ApproveTests : TransactionServiceTestBase
{
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
}
