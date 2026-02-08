namespace Domain.Enums;

/// <summary>
/// Status of a transaction in its lifecycle
/// </summary>
public enum TransactionStatus
{
    /// <summary>
    /// Transaction created, awaiting processing
    /// </summary>
    Pending = 1,

    /// <summary>
    /// Transaction is being processed
    /// </summary>
    Processing = 2,

    /// <summary>
    /// Transaction completed successfully
    /// </summary>
    Completed = 3,

    /// <summary>
    /// Transaction failed due to error
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Transaction cancelled by user or system
    /// </summary>
    Cancelled = 5,

    /// <summary>
    /// Transaction rejected by operator or compliance
    /// </summary>
    Rejected = 6
}