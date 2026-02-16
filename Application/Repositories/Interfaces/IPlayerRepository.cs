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
    /// <summary>
    /// Get audit logs for a specific user
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get audit logs for a specific entity
    /// </summary>
    Task<IEnumerable<AuditLog>> GetByEntityAsync(string entityType, Guid entityId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Notification repository interface
/// </summary>
public interface INotificationRepository : IRepository<Notification>
{
    /// <summary>
    /// Get unread notifications for a user
    /// </summary>
    Task<IEnumerable<Notification>> GetUnreadByUserIdAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Mark notification as read
    /// </summary>
    Task MarkAsReadAsync(Guid notificationId, CancellationToken cancellationToken = default);
}
