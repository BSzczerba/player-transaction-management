using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implementations;

public class NotificationService : INotificationService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<NotificationService> _log;

    public NotificationService(IUnitOfWork uow, IMapper mapper, ILogger<NotificationService> log)
    {
        _uow = uow;
        _mapper = mapper;
        _log = log;
    }

    public async Task<NotificationDto> CreateAsync(Guid userId, string type, string title, string message,
        string? relatedEntityType = null, Guid? relatedEntityId = null, CancellationToken ct = default)
    {
        var notification = new Notification
        {
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IsRead = false,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId
        };

        await _uow.Notifications.AddAsync(notification, ct);
        await _uow.SaveChangesAsync(ct);

        _log.LogDebug("Notification created for user {UserId}: {Title}", userId, title);

        return _mapper.Map<NotificationDto>(notification);
    }

    public async Task<IEnumerable<NotificationDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await _uow.Notifications.GetByUserIdAsync(userId, ct);
        return _mapper.Map<IEnumerable<NotificationDto>>(notifications);
    }

    public async Task<IEnumerable<NotificationDto>> GetUnreadByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var notifications = await _uow.Notifications.GetUnreadByUserIdAsync(userId, ct);
        return _mapper.Map<IEnumerable<NotificationDto>>(notifications);
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken ct = default)
    {
        return await _uow.Notifications.GetUnreadCountAsync(userId, ct);
    }

    public async Task MarkAsReadAsync(Guid notificationId, Guid userId, CancellationToken ct = default)
    {
        await _uow.Notifications.MarkAsReadAsync(notificationId, ct);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken ct = default)
    {
        await _uow.Notifications.MarkAllAsReadAsync(userId, ct);
        await _uow.SaveChangesAsync(ct);
    }
}
