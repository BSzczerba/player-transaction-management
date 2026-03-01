using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implementations;

public class AuditService : IAuditService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<AuditService> _log;

    public AuditService(IUnitOfWork uow, IMapper mapper, ILogger<AuditService> log)
    {
        _uow = uow;
        _mapper = mapper;
        _log = log;
    }

    public async Task LogAsync(Guid? userId, string action, string entityType, Guid? entityId,
        string? ipAddress = null, string? userAgent = null, string? oldValues = null,
        string? newValues = null, string? details = null, CancellationToken ct = default)
    {
        var auditLog = new AuditLog
        {
            UserId = userId,
            Action = action,
            EntityType = entityType,
            EntityId = entityId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            OldValues = oldValues,
            NewValues = newValues,
            Details = details ?? $"{action} at {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}"
        };

        await _uow.AuditLogs.AddAsync(auditLog, ct);

        _log.LogDebug("Audit: {Action} by {UserId} on {EntityType}/{EntityId}",
            action, userId, entityType, entityId);
    }

    public async Task<IEnumerable<AuditLogDto>> GetByUserAsync(Guid userId, CancellationToken ct = default)
    {
        var logs = await _uow.AuditLogs.GetByUserIdAsync(userId, ct);
        return _mapper.Map<IEnumerable<AuditLogDto>>(logs);
    }

    public async Task<IEnumerable<AuditLogDto>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default)
    {
        var logs = await _uow.AuditLogs.GetByEntityAsync(entityType, entityId, ct);
        return _mapper.Map<IEnumerable<AuditLogDto>>(logs);
    }

    public async Task<PagedResult<AuditLogDto>> GetAllAsync(AuditLogFilterDto filter, CancellationToken ct = default)
    {
        var (items, totalCount) = await _uow.AuditLogs.GetFilteredAsync(filter, ct);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        return new PagedResult<AuditLogDto>
        {
            Items = _mapper.Map<IEnumerable<AuditLogDto>>(items),
            TotalCount = totalCount,
            Page = filter.Page,
            PageSize = pageSize
        };
    }
}
