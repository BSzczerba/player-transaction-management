using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Represents a financial transaction (deposit or withdrawal)
/// </summary>
public class Transaction : BaseEntity
{
    /// <summary>
    /// ID of the player who made the transaction
    /// </summary>
    public Guid PlayerId { get; set; }

    /// <summary>
    /// Navigation property: Player
    /// </summary>
    public Player Player { get; set; } = null!;

    /// <summary>
    /// Type of transaction (Deposit/Withdrawal)
    /// </summary>
    public TransactionType Type { get; set; }

    /// <summary>
    /// Transaction amount
    /// </summary>
    public decimal Amount { get; set; }

    /// <summary>
    /// Current status of transaction
    /// </summary>
    public TransactionStatus Status { get; set; } = TransactionStatus.Pending;

    /// <summary>
    /// ID of payment method used
    /// </summary>
    public Guid? PaymentMethodId { get; set; }

    /// <summary>
    /// Navigation property: Payment method
    /// </summary>
    public PaymentMethod? PaymentMethod { get; set; }

    /// <summary>
    /// Reference number from payment gateway
    /// </summary>
    public string? PaymentGatewayReference { get; set; }

    /// <summary>
    /// Description or notes about the transaction
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// ID of operator who approved/rejected (if applicable)
    /// </summary>
    public Guid? ApprovedById { get; set; }

    /// <summary>
    /// Navigation property: Operator who approved
    /// </summary>
    public Player? ApprovedBy { get; set; }

    /// <summary>
    /// Timestamp when transaction was approved
    /// </summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Reason for rejection (if rejected)
    /// </summary>
    public string? RejectionReason { get; set; }

    /// <summary>
    /// Timestamp when transaction was completed
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// IP address from which transaction was initiated
    /// </summary>
    public string? IpAddress { get; set; }

    /// <summary>
    /// Whether this transaction was flagged as suspicious
    /// </summary>
    public bool IsFlagged { get; set; }

    /// <summary>
    /// Reason for flagging (if flagged)
    /// </summary>
    public string? FlagReason { get; set; }

    /// <summary>
    /// Balance before this transaction
    /// </summary>
    public decimal? BalanceBefore { get; set; }

    /// <summary>
    /// Balance after this transaction
    /// </summary>
    public decimal? BalanceAfter { get; set; }
}