namespace Domain.Enums;

/// <summary>
/// Status of a player account
/// </summary>
public enum AccountStatus
{
    /// <summary>
    /// Account is active and can perform transactions
    /// </summary>
    Active = 1,

    /// <summary>
    /// Account is suspended, no transactions allowed
    /// </summary>
    Suspended = 2,

    /// <summary>
    /// Account is closed permanently
    /// </summary>
    Closed = 3,

    /// <summary>
    /// Account is pending verification (KYC)
    /// </summary>
    PendingVerification = 4
}