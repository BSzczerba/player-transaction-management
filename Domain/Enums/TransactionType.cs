namespace Domain.Enums;

/// <summary>
/// Type of transaction
/// </summary>
public enum TransactionType
{
    /// <summary>
    /// Deposit - adding funds to player account
    /// </summary>
    Deposit = 1,

    /// <summary>
    /// Withdrawal - removing funds from player account
    /// </summary>
    Withdrawal = 2
}