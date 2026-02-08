using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// Represents a payment method available in the system
/// </summary>
public class PaymentMethod : BaseEntity
{
    /// <summary>
    /// Display name of the payment method
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Type of payment method
    /// </summary>
    public PaymentMethodType Type { get; set; }

    /// <summary>
    /// Whether this payment method is currently active
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Minimum transaction amount for this method
    /// </summary>
    public decimal MinAmount { get; set; } = 10;

    /// <summary>
    /// Maximum transaction amount for this method
    /// </summary>
    public decimal MaxAmount { get; set; } = 100000;

    /// <summary>
    /// Processing fee percentage
    /// </summary>
    public decimal FeePercentage { get; set; }

    /// <summary>
    /// Fixed processing fee
    /// </summary>
    public decimal FixedFee { get; set; }

    /// <summary>
    /// Typical processing time in minutes
    /// </summary>
    public int ProcessingTimeMinutes { get; set; }

    /// <summary>
    /// Logo URL
    /// </summary>
    public string? LogoUrl { get; set; }

    /// <summary>
    /// Navigation property: Transactions using this method
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
}