using Application.DTOs;
using Domain.Entities;

namespace Application.Repositories.Interfaces;

/// <summary>
/// Player repository interface
/// </summary>
public interface IPlayerRepository : IRepository<Player>
{
    /// <summary>
    /// Get player by email
    /// </summary>
    Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player by username
    /// </summary>
    Task<Player?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if email exists
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if username exists
    /// </summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get players by role
    /// </summary>
    Task<IEnumerable<Player>> GetByRoleAsync(Domain.Enums.UserRole role, CancellationToken cancellationToken = default);
}

/// <summary>
/// Transaction repository interface
/// </summary>
public interface ITransactionRepository : IRepository<Transaction>
{
    /// <summary>
    /// Get transactions for a specific player
    /// </summary>
    Task<IEnumerable<Transaction>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending transactions
    /// </summary>
    Task<IEnumerable<Transaction>> GetPendingTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get flagged transactions
    /// </summary>
    Task<IEnumerable<Transaction>> GetFlaggedTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player's transactions for today
    /// </summary>
    Task<IEnumerable<Transaction>> GetTodaysTransactionsByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player's transactions from the last 24 hours (rolling window, translated to SQL)
    /// </summary>
    Task<IEnumerable<Transaction>> GetLast24HoursTransactionsByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get filtered and paginated transactions
    /// </summary>
    Task<(IEnumerable<Transaction> Items, int TotalCount)> GetFilteredAsync(TransactionFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get compliance summary with SQL-level aggregation
    /// </summary>
    Task<ComplianceSummaryDto> GetComplianceSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player risk stats with SQL-level aggregation
    /// </summary>
    Task<PlayerRiskStatsDto> GetPlayerRiskStatsAsync(Guid playerId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Payment method repository interface
/// </summary>
public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
    /// <summary>
    /// Get all active payment methods
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Audit log repository interface
/// </summary>
public interface IAuditLogRepository : IRepository<AuditLog>
{
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
    Task<(IEnumerable<AuditLog> Items, int TotalCount)> GetFilteredAsync(AuditLogFilterDto filter, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification repository interface
/// </summary>
public interface INotificationRepository : IRepository<Notification>
{
    Task<IEnumerable<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Notification>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
    Task MarkAllAsReadAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(Guid userId, CancellationToken cancellationToken = default);
}
