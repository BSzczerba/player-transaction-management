using Application.Models;
using Application.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;

/// <summary>
/// Mock payment gateway that simulates real-world processing behavior:
/// realistic failure rates per payment method type, random gateway reference IDs,
/// and simulated network latency.
///
/// In production, replace this with an adapter for the actual payment processor
/// (e.g., Stripe, PaySafe, Adyen) implementing the same <see cref="IPaymentGatewayService"/> interface.
/// </summary>
public class MockPaymentGatewayService : IPaymentGatewayService
{
    private readonly ILogger<MockPaymentGatewayService> _log;

    // Probability of failure per payment method type (0.0 = never fails, 1.0 = always fails)
    private static readonly Dictionary<string, double> FailureRates = new(StringComparer.OrdinalIgnoreCase)
    {
        { "CreditCard",     0.05 },
        { "BankTransfer",   0.02 },
        { "PayPal",         0.03 },
        { "Skrill",         0.04 },
        { "Neteller",       0.04 },
        { "Cryptocurrency", 0.08 }
    };

    // Error codes grouped by payment method category
    private static readonly (string Code, string Message)[] CardErrors =
    [
        ("CARD_DECLINED",        "The card was declined by the issuing bank."),
        ("INSUFFICIENT_FUNDS",   "Insufficient funds on the card."),
        ("EXPIRED_CARD",         "The card has expired."),
        ("FRAUD_SUSPECT",        "Transaction flagged as potentially fraudulent by the issuer.")
    ];

    private static readonly (string Code, string Message)[] BankErrors =
    [
        ("BANK_UNAVAILABLE",     "The bank is temporarily unavailable. Please try again later."),
        ("INVALID_ACCOUNT",      "The destination bank account is invalid."),
        ("TRANSFER_LIMIT",       "Bank transfer limit exceeded for this period.")
    ];

    public MockPaymentGatewayService(ILogger<MockPaymentGatewayService> log)
    {
        _log = log;
    }

    /// <inheritdoc />
    public async Task<PaymentGatewayResult> ProcessPaymentAsync(
        Guid transactionId,
        decimal amount,
        string paymentMethodType,
        CancellationToken ct = default)
    {
        // Simulate network latency (50–250 ms)
        await Task.Delay(Random.Shared.Next(50, 250), ct);

        var failureRate = FailureRates.TryGetValue(paymentMethodType, out var rate) ? rate : 0.05;

        if (Random.Shared.NextDouble() < failureRate)
        {
            var (code, message) = PickError(paymentMethodType);
            _log.LogWarning(
                "Mock gateway: payment FAILED for transaction {TransactionId} ({PaymentMethod}): [{ErrorCode}] {ErrorMessage}",
                transactionId, paymentMethodType, code, message);

            return PaymentGatewayResult.Failed(code, message);
        }

        var gatewayRef = GenerateGatewayReference("PAY");
        _log.LogInformation(
            "Mock gateway: payment SUCCEEDED for transaction {TransactionId} ({PaymentMethod}): ref={GatewayRef}",
            transactionId, paymentMethodType, gatewayRef);

        return PaymentGatewayResult.Succeeded(gatewayRef);
    }

    /// <inheritdoc />
    public async Task<PaymentGatewayResult> ProcessRefundAsync(
        string gatewayTransactionId,
        decimal amount,
        CancellationToken ct = default)
    {
        // Simulate network latency (50–150 ms)
        await Task.Delay(Random.Shared.Next(50, 150), ct);

        // Refunds have a very high success rate (99 %)
        if (Random.Shared.NextDouble() < 0.01)
        {
            _log.LogWarning(
                "Mock gateway: refund FAILED for original transaction {GatewayTransactionId}",
                gatewayTransactionId);

            return PaymentGatewayResult.Failed("REFUND_FAILED", "Refund could not be processed by the gateway.");
        }

        var refundRef = GenerateGatewayReference("REF");
        _log.LogInformation(
            "Mock gateway: refund SUCCEEDED for original transaction {GatewayTransactionId}: ref={RefundRef}",
            gatewayTransactionId, refundRef);

        return PaymentGatewayResult.Succeeded(refundRef);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static string GenerateGatewayReference(string prefix)
    {
        // e.g. "PAY-4A3B2C1D0E9F8A7B"
        var hex = Guid.NewGuid().ToString("N").ToUpper()[..16];
        return $"{prefix}-{hex}";
    }

    private static (string Code, string Message) PickError(string paymentMethodType)
    {
        var isCardBased = paymentMethodType is "CreditCard" or "Skrill" or "Neteller" or "PayPal";
        var pool = isCardBased ? CardErrors : BankErrors;
        return pool[Random.Shared.Next(pool.Length)];
    }
}
