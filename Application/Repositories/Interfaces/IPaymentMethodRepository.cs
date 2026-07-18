using Domain.Entities;

namespace Application.Repositories.Interfaces;

/// <summary>
/// Payment method repository interface
/// </summary>
public interface IPaymentMethodRepository : IRepository<PaymentMethod>
{
    /// <summary>
    /// Get all active payment methods
    /// </summary>
    Task<IEnumerable<PaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken cancellationToken = default);
}
