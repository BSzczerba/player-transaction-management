using Domain.Entities;
using Infrastructure.Data;
using Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implementations;

public class PaymentMethodRepository : Repository<PaymentMethod>, IPaymentMethodRepository
{
    public PaymentMethodRepository(ApplicationDbContext context) : base(context) { }

    public async Task<IEnumerable<PaymentMethod>> GetActivePaymentMethodsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(pm => pm.IsActive)
            .OrderBy(pm => pm.Name)
            .ToListAsync(cancellationToken);
    }
}
