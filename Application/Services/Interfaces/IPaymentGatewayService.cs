using Application.Models;

namespace Application.Services.Interfaces;

/// <summary>
/// Abstraction over an external payment gateway.
/// The mock implementation is used in this project; a real gateway adapter would implement the same interface.
/// </summary>
public interface IPaymentGatewayService
{
    /// <summary>
    /// Submits a payment (deposit or withdrawal) to the gateway for processing.
    /// </summary>
    Task<PaymentGatewayResult> ProcessPaymentAsync(
        Guid transactionId,
        decimal amount,
        string paymentMethodType,
        CancellationToken ct = default);

    /// <summary>
    /// Requests a refund for a previously processed gateway transaction.
    /// </summary>
    Task<PaymentGatewayResult> ProcessRefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default);
}
