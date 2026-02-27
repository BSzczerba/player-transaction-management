using Application.DTOs;

namespace Application.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(Guid? userId, string action, string entityType, Guid? entityId,
        string? ipAddress = null, string? userAgent = null, string? oldValues = null,
        string? newValues = null, string? details = null, CancellationToken ct = default);

    Task<IEnumerable<AuditLogDto>> GetByUserAsync(Guid userId, CancellationToken ct = default);

    Task<IEnumerable<AuditLogDto>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken ct = default);

    Task<PagedResult<AuditLogDto>> GetAllAsync(AuditLogFilterDto filter, CancellationToken ct = default);
}
