namespace Application.Models;

/// <summary>
/// Result returned by the payment gateway after processing a payment or refund.
/// </summary>
public class PaymentGatewayResult
{
    public bool Success { get; private set; }

    /// <summary>
    /// Unique reference ID assigned by the gateway (populated on success).
    /// </summary>
    public string GatewayTransactionId { get; private set; } = string.Empty;

    /// <summary>
    /// Machine-readable error code (populated on failure).
    /// </summary>
    public string? ErrorCode { get; private set; }

    /// <summary>
    /// Human-readable error description (populated on failure).
    /// </summary>
    public string? ErrorMessage { get; private set; }

    public DateTime ProcessedAt { get; private set; }

    public static PaymentGatewayResult Succeeded(string gatewayTransactionId) => new()
    {
        Success = true,
        GatewayTransactionId = gatewayTransactionId,
        ProcessedAt = DateTime.UtcNow
    };

    public static PaymentGatewayResult Failed(string errorCode, string errorMessage) => new()
    {
        Success = false,
        ErrorCode = errorCode,
        ErrorMessage = errorMessage,
        ProcessedAt = DateTime.UtcNow
    };
}
