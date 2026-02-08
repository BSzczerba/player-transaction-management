using Domain.Enums;
using System.Transactions;

namespace Domain.Entities;

/// <summary>
/// Represents a player in the system
/// </summary>
public class Player : BaseEntity
{
    /// <summary>
    /// Player's email address (unique)
    /// </summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Player's username (unique)
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Hashed password
    /// </summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// Player's first name
    /// </summary>
    public string FirstName { get; set; } = string.Empty;

    /// <summary>
    /// Player's last name
    /// </summary>
    public string LastName { get; set; } = string.Empty;

    /// <summary>
    /// Phone number
    /// </summary>
    public string? PhoneNumber { get; set; }

    /// <summary>
    /// Date of birth
    /// </summary>
    public DateTime? DateOfBirth { get; set; }

    /// <summary>
    /// Player's role in the system
    /// </summary>
    public UserRole Role { get; set; } = UserRole.Player;

    /// <summary>
    /// Account status
    /// </summary>
    public AccountStatus Status { get; set; } = AccountStatus.PendingVerification;

    /// <summary>
    /// Current balance
    /// </summary>
    public decimal Balance { get; set; }

    /// <summary>
    /// Daily deposit limit
    /// </summary>
    public decimal DailyDepositLimit { get; set; } = 10000;

    /// <summary>
    /// Daily withdrawal limit
    /// </summary>
    public decimal DailyWithdrawalLimit { get; set; } = 5000;

    /// <summary>
    /// Whether email is verified
    /// </summary>
    public bool EmailVerified { get; set; }

    /// <summary>
    /// Whether phone is verified
    /// </summary>
    public bool PhoneVerified { get; set; }

    /// <summary>
    /// Whether KYC is completed
    /// </summary>
    public bool KycVerified { get; set; }

    /// <summary>
    /// Last login timestamp
    /// </summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Navigation property: Player's transactions
    /// </summary>
    public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();

    /// <summary>
    /// Navigation property: Audit logs related to this player
    /// </summary>
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}