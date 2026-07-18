using Application.DTOs;
using Domain.Entities;

namespace Application.Repositories.Interfaces;

/// <summary>
/// Audit log repository interface
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetFilteredAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);
}
