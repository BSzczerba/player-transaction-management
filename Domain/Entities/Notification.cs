namespace Domain.Entities;

/// <summary>
/// Represents a notification sent to a user
/// </summary>
public class Notification : BaseEntity
{
    /// <summary>
    /// ID of the user receiving the notification
    /// </summary>
    public Guid UserId { get; set; }

    /// <summary>
    /// Navigation property: User
    /// </summary>
    public Player User { get; set; } = null!;

    /// <summary>
    /// Type of notification (e.g., "TransactionCompleted", "AccountSuspended")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Notification title
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Notification message
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// Whether the notification has been read
    /// </summary>
    public bool IsRead { get; set; }

    /// <summary>
    /// When the notification was read
    /// </summary>
    public DateTime? ReadAt { get; set; }

    /// <summary>
    /// Related entity type (e.g., "Transaction")
    /// </summary>
    public string? RelatedEntityType { get; set; }

    /// <summary>
    /// Related entity ID
    /// </summary>
    public Guid? RelatedEntityId { get; set; }

    /// <summary>
    /// Whether email was sent
    /// </summary>
    public bool EmailSent { get; set; }

    /// <summary>
    /// When email was sent
    /// </summary>
    public DateTime? EmailSentAt { get; set; }
}