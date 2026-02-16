
namespace Application.Repositories.Interfaces;

/// <summary>
/// Unit of Work pattern interface for managing transactions
/// </summary>
public interface IUnitOfWork : IDisposable
{
    /// <summary>
    /// Player repository
    /// </summary>
    IPlayerRepository Players { get; }

    /// <summary>
    /// Transaction repository
    /// </summary>
    ITransactionRepository Transactions { get; }

    /// <summary>
    /// Payment method repository
    /// </summary>
    IPaymentMethodRepository PaymentMethods { get; }

    /// <summary>
    /// Audit log repository
    /// </summary>
    IAuditLogRepository AuditLogs { get; }

    /// <summary>
    /// Notification repository
    /// </summary>
    INotificationRepository Notifications { get; }

    /// <summary>
    /// Save all changes to the database
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Begin database transaction
    /// </summary>
    Task BeginTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Commit database transaction
    /// </summary>
    Task CommitTransactionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Rollback database transaction
    /// </summary>
    Task RollbackTransactionAsync(CancellationToken cancellationToken = default);
}
