namespace Domain.Enums;

/// <summary>
/// Defines the roles available in the system
/// </summary>
public enum UserRole
{
    /// <summary>
    /// Player - can make deposits and withdrawals
    /// </summary>
    Player = 1,

    /// <summary>
    /// Operator - can approve/reject transactions
    /// </summary>
    Operator = 2,

    /// <summary>
    /// Administrator - full system access
    /// </summary>
    Administrator = 3,

    /// <summary>
    /// Compliance Officer - monitors AML and suspicious activities
    /// </summary>
    ComplianceOfficer = 4
}