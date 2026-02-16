using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implementations;

/// <summary>
/// Transaction repository implementation
/// </summary>
public class TransactionRepository : Repository<Transaction>, ITransactionRepository
{
    public TransactionRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Transaction>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.PlayerId == playerId)
            .Include(t => t.PaymentMethod)
            .Include(t => t.ApprovedBy)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetPendingTransactionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.Status == TransactionStatus.Pending)
            .Include(t => t.Player)
            .Include(t => t.PaymentMethod)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetFlaggedTransactionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(t => t.IsFlagged)
            .Include(t => t.Player)
            .Include(t => t.PaymentMethod)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetTodaysTransactionsByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);

        return await _dbSet
            .Where(t => t.PlayerId == playerId &&
                       t.CreatedAt >= today &&
                       t.CreatedAt < tomorrow)
            .ToListAsync(cancellationToken);
    }
}
