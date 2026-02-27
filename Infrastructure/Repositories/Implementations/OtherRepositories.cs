using Application.DTOs;
using Domain.Entities;
using Infrastructure.Data;
using Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implementations;

public class PaymentMethodRepository : Repository<PaymentMethod>, IPaymentMethodRepository
{
    public PaymentMethodRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(pm => pm.IsActive)
            .OrderBy(pm => pm.Name)
            .ToListAsync(cancellationToken);
    }
}

public class AuditLogRepository : Repository<AuditLog>, IAuditLogRepository
{
    public AuditLogRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.User)
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Include(a => a.User)
            .Where(a => a.EntityType == entityType && a.EntityId == entityId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetFilteredAsync(
        AuditLogFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Include(a => a.User).AsQueryable();

        if (filter.UserId.HasValue)
            query = query.Where(a => a.UserId == filter.UserId.Value);
        if (!string.IsNullOrEmpty(filter.Action))
            query = query.Where(a => a.Action.Contains(filter.Action));
        if (!string.IsNullOrEmpty(filter.EntityType))
            query = query.Where(a => a.EntityType == filter.EntityType);
        if (filter.StartDate.HasValue)
            query = query.Where(a => a.CreatedAt >= filter.StartDate.Value);
        if (filter.EndDate.HasValue)
            query = query.Where(a => a.CreatedAt <= filter.EndDate.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var page = Math.Max(1, filter.Page);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}

public class NotificationRepository : Repository<Notification>, INotificationRepository
{
    public NotificationRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default)
    {
        var notification = await GetByIdAsync(notificationId, cancellationToken);
        if (notification != null)
        {
            notification.IsRead = true;
            notification.ReadAt = DateTime.UtcNow;
            Update(notification);
        }
    }

    public async Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = await _dbSet
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(cancellationToken);

        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = DateTime.UtcNow;
        }
    }

    public async Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);
    }
}
