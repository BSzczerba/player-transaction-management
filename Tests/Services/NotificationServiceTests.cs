using Application.Repositories.Interfaces;
using Application.Services.Implementations;
using AutoMapper;
using Domain.Entities;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Regression tests for the two documented <see cref="NotificationService"/> SaveChanges
/// invariants (see CLAUDE.md): <c>CreateAsync</c> must never flush on its own because it is
/// always called inside a <see cref="TransactionService"/> transaction, while
/// <c>MarkAsReadAsync</c> is a standalone operation and must flush immediately.
/// </summary>
public class NotificationServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<INotificationRepository> _notifications = new();
    private readonly IMapper _mapper;

    private readonly NotificationService _sut;

    private static readonly Guid UserId = Guid.NewGuid();

    public NotificationServiceTests()
    {
        _uow.Setup(u => u.Notifications).Returns(_notifications.Object);

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<Application.Mappings.MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _sut = new NotificationService(_uow.Object, _mapper, NullLogger<NotificationService>.Instance);
    }

    [Fact]
    public async Task CreateAsync_NeverCallsSaveChanges()
    {
        // Arrange
        _notifications.Setup(r => r.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Notification n, CancellationToken _) => n);

        // Act
        await _sut.CreateAsync(UserId, "AmlFlag", "Transaction flagged", "A transaction was flagged for review.");

        // Assert — the caller's DB transaction owns the flush; a stray SaveChanges here would
        // commit a partial transaction if a later step in the caller's flow fails.
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MarkAsReadAsync_OwnNotification_CallsSaveChanges()
    {
        // Arrange
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = UserId,
            Type = "AmlFlag",
            Title = "Transaction flagged",
            Message = "msg",
            IsRead = false
        };
        _notifications.Setup(r => r.GetByIdAsync(notification.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(notification);
        _notifications.Setup(r => r.Update(It.IsAny<Notification>()));

        // Act
        await _sut.MarkAsReadAsync(notification.Id, UserId);

        // Assert
        notification.IsRead.Should().BeTrue();
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
