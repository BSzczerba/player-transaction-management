using Application.DTOs;
using Application.Repositories.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
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
            .AsNoTracking()
            .Where(t => t.PlayerId == playerId)
            .Include(t => t.PaymentMethod)
            .Include(t => t.ApprovedBy)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetPendingTransactionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.Status == TransactionStatus.Pending)
            .Include(t => t.Player)
            .Include(t => t.PaymentMethod)
            .OrderBy(t => t.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetFlaggedTransactionsAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
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
            .AsNoTracking()
            .Where(t => t.PlayerId == playerId &&
                       t.CreatedAt >= today &&
                       t.CreatedAt < tomorrow)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<Transaction>> GetLast24HoursTransactionsByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        var threshold = DateTime.UtcNow.AddHours(-24);

        return await _dbSet
            .AsNoTracking()
            .Where(t => t.PlayerId == playerId && t.CreatedAt >= threshold)
            .ToListAsync(cancellationToken);
    }

    public async Task<(IEnumerable<Transaction> Items, int TotalCount)> GetFilteredAsync(
        TransactionFilterDto filter,
        CancellationToken cancellationToken = default)
    {
        var query = _dbSet
            .AsNoTracking()
            .Include(t => t.Player)
            .Include(t => t.PaymentMethod)
            .Include(t => t.ApprovedBy)
            .AsQueryable();

        if (filter.PlayerId.HasValue)
            query = query.Where(t => t.PlayerId == filter.PlayerId.Value);

        if (filter.Type.HasValue)
            query = query.Where(t => t.Type == filter.Type.Value);

        if (filter.Status.HasValue)
            query = query.Where(t => t.Status == filter.Status.Value);

        if (filter.StartDate.HasValue)
            query = query.Where(t => t.CreatedAt >= filter.StartDate.Value);

        if (filter.EndDate.HasValue)
            query = query.Where(t => t.CreatedAt <= filter.EndDate.Value);

        if (filter.MinAmount.HasValue)
            query = query.Where(t => t.Amount >= filter.MinAmount.Value);

        if (filter.MaxAmount.HasValue)
            query = query.Where(t => t.Amount <= filter.MaxAmount.Value);

        if (filter.IsFlagged.HasValue)
            query = query.Where(t => t.IsFlagged == filter.IsFlagged.Value);

        var totalCount = await query.CountAsync(cancellationToken);

        var pageSize = Math.Clamp(filter.PageSize, 1, 100);
        var page = Math.Max(filter.Page, 1);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ComplianceSummaryDto> GetComplianceSummaryAsync(CancellationToken cancellationToken = default)
    {
        var summary = await _dbSet
            .AsNoTracking()
            .Where(t => t.IsFlagged)
            .GroupBy(_ => 1)
            .Select(g => new ComplianceSummaryDto
            {
                TotalFlaggedTransactions = g.Count(),
                PendingReviewCount = g.Count(t => t.Status == TransactionStatus.Pending),
                TotalFlaggedAmount = g.Sum(t => t.Amount),
                FlaggedPlayersCount = g.Select(t => t.PlayerId).Distinct().Count()
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new ComplianceSummaryDto();

        // Top flagged players — limited to 10 at SQL level
        summary.TopFlaggedPlayers = await _dbSet
            .AsNoTracking()
            .Where(t => t.IsFlagged)
            .GroupBy(t => new { t.PlayerId, t.Player!.Username })
            .Select(g => new FlaggedPlayerSummaryDto
            {
                PlayerId = g.Key.PlayerId,
                Username = g.Key.Username,
                FlaggedTransactionCount = g.Count(),
                TotalFlaggedAmount = g.Sum(t => t.Amount),
                LatestFlagReason = g.OrderByDescending(t => t.CreatedAt).First().FlagReason
            })
            .OrderByDescending(p => p.FlaggedTransactionCount)
            .Take(10)
            .ToListAsync(cancellationToken);

        return summary;
    }

    public async Task<PlayerRiskStatsDto> GetPlayerRiskStatsAsync(Guid playerId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.PlayerId == playerId)
            .GroupBy(_ => 1)
            .Select(g => new PlayerRiskStatsDto
            {
                TotalTransactions = g.Count(),
                FlaggedTransactions = g.Count(t => t.IsFlagged),
                TotalDeposited = g
                    .Where(t => t.Type == TransactionType.Deposit && t.Status == TransactionStatus.Completed)
                    .Sum(t => t.Amount),
                TotalWithdrawn = g
                    .Where(t => t.Type == TransactionType.Withdrawal && t.Status == TransactionStatus.Completed)
                    .Sum(t => t.Amount)
            })
            .FirstOrDefaultAsync(cancellationToken) ?? new PlayerRiskStatsDto();
    }
}
