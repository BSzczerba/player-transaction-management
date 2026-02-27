using Application.DTOs;

namespace Application.Services.Interfaces;

public interface INotificationService
{
    Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default);

    Task<IEnumerable<NotificationDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    Task<IEnumerable<NotificationDto>> GetUnreadByUserAsync(Guid userId, CancellationToken ct = default);

    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default);

    Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default);

    Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default);
}
