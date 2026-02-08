namespace Domain.Entities;

/// <summary>
/// Represents an audit log entry for tracking all system actions
/// </summary>
public class AuditLog : BaseEntity
{
    /// <summary>
    /// ID of the user who performed the action
    /// </summary>
    public Guid? UserId { get; set; }

    /// <summary>
    /// Navigation property: User
    /// </summary>
    public Player? User { get; set; }

    /// <summary>
    /// Action performed (e.g., "CreateTransaction", "ApproveWithdrawal")
    /// </summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// Entity type affected (e.g., "Transaction", "Player")
    /// </summary>
    public string EntityType { get; set; } = string.Empty;

    /// <summary>
    /// ID of the entity affected
    /// </summary>
    public Guid? EntityId { get; set; }

    /// <summary>
    /// Old values (JSON)
    /// </summary>
    public string? OldValues { get; set; }

    /// <summary>
    /// New values (JSON)
    /// </summary>
    public string? NewValues { get; set; }

    /// <summary>
    /// IP address from which action was performed
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// User agent string
    /// </summary>
    public string? UserAgent { get; set; }

    /// <summary>
    /// Additional details about the action
    /// </summary>
    public string? Details { get; set; }
}