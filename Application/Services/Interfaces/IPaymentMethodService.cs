using Application.DTOs;

namespace Application.Services.Interfaces;

/// <summary>
/// Service for payment method operations
/// </summary>
public interface IPaymentMethodService
{
    /// <summary>
    /// Get all active payment methods
    /// </summary>
    Task<IEnumerable<PaymentMethodDto>> GetActiveAsync(CancellationToken ct = default);

    /// <summary>
    /// Get a payment method by ID
    /// </summary>
    Task<PaymentMethodDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
}
