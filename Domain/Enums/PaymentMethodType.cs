namespace Domain.Enums;

/// <summary>
/// Type of payment method
/// </summary>
public enum PaymentMethodType
{
    /// <summary>
    /// Credit or debit card
    /// </summary>
    CreditCard = 1,

    /// <summary>
    /// Bank transfer
    /// </summary>
    BankTransfer = 2,

    /// <summary>
    /// PayPal payment
    /// </summary>
    PayPal = 3,

    /// <summary>
    /// Skrill e-wallet
    /// </summary>
    Skrill = 4,

    /// <summary>
    /// Neteller e-wallet
    /// </summary>
    Neteller = 5,

    /// <summary>
    /// Cryptocurrency payment
    /// </summary>
    Cryptocurrency = 6
}