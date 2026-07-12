using Domain.Entities;
using FluentAssertions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="Application.Services.Implementations.TransactionService.RejectAsync"/>.
/// </summary>
public class RejectTests : TransactionServiceTestBase
{
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
        transaction.Status = Domain.Enums.TransactionStatus.Completed;

        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var act = () => _sut.RejectAsync(transaction.Id, OperatorId, "reason");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*status*");
    }

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
}
