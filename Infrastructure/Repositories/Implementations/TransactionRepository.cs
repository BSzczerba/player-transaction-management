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

    // ── Reports ──────────────────────────────────────────────────────────────

    public async Task<TransactionSummaryRawDto> GetFinancialSummaryRawAsync(
        DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .GroupBy(_ => 1)
            .Select(g => new TransactionSummaryRawDto
            {
                TotalDeposits = g.Sum(t =>
                    t.Type == TransactionType.Deposit && t.Status == TransactionStatus.Completed ? t.Amount : 0m),
                TotalWithdrawals = g.Sum(t =>
                    t.Type == TransactionType.Withdrawal && t.Status == TransactionStatus.Completed ? t.Amount : 0m),
                CompletedCount = g.Count(t => t.Status == TransactionStatus.Completed),
                PendingCount = g.Count(t => t.Status == TransactionStatus.Pending),
                ProcessingCount = g.Count(t => t.Status == TransactionStatus.Processing),
                RejectedCount = g.Count(t => t.Status == TransactionStatus.Rejected),
                FlaggedCount = g.Count(t => t.IsFlagged),
                TotalCount = g.Count()
            })
            .FirstOrDefaultAsync(ct) ?? new TransactionSummaryRawDto();
    }

    public async Task<IEnumerable<DailyTransactionStatsDto>> GetDailyStatsAsync(
        DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate)
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new DailyTransactionStatsDto
            {
                Date = g.Key,
                Deposits = g.Sum(t =>
                    t.Type == TransactionType.Deposit && t.Status == TransactionStatus.Completed ? t.Amount : 0m),
                Withdrawals = g.Sum(t =>
                    t.Type == TransactionType.Withdrawal && t.Status == TransactionStatus.Completed ? t.Amount : 0m),
                TransactionCount = g.Count(),
                FlaggedCount = g.Count(t => t.IsFlagged)
            })
            .OrderBy(d => d.Date)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<TopPlayerDto>> GetTopPlayersByVolumeAsync(
        DateTime startDate, DateTime endDate, int limit, CancellationToken ct = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate
                     && t.Status == TransactionStatus.Completed)
            .GroupBy(t => new { t.PlayerId, t.Player!.Username, t.Player.Balance })
            .Select(g => new TopPlayerDto
            {
                PlayerId = g.Key.PlayerId,
                Username = g.Key.Username,
                TotalVolume = g.Sum(t => t.Amount),
                TransactionCount = g.Count(),
                CurrentBalance = g.Key.Balance
            })
            .OrderByDescending(p => p.TotalVolume)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IEnumerable<PaymentMethodStatsDto>> GetPaymentMethodStatsAsync(
        DateTime startDate, DateTime endDate, CancellationToken ct = default)
    {
        // Pull enum type as int from SQL, convert to string in memory
        var raw = await _dbSet
            .AsNoTracking()
            .Where(t => t.CreatedAt >= startDate && t.CreatedAt <= endDate
                     && t.PaymentMethodId != null)
            .GroupBy(t => new { t.PaymentMethodId, t.PaymentMethod!.Name, t.PaymentMethod.Type })
            .Select(g => new
            {
                PaymentMethodId = g.Key.PaymentMethodId!.Value,
                g.Key.Name,
                g.Key.Type,
                TransactionCount = g.Count(),
                TotalVolume = g.Sum(t => t.Amount),
                AverageAmount = g.Average(t => t.Amount)
            })
            .OrderByDescending(p => p.TotalVolume)
            .ToListAsync(ct);

        return raw.Select(r => new PaymentMethodStatsDto
        {
            PaymentMethodId = r.PaymentMethodId,
            Name = r.Name,
            Type = r.Type.ToString(),
            TransactionCount = r.TransactionCount,
            TotalVolume = r.TotalVolume,
            AverageAmount = r.AverageAmount
        });
    }

    public async Task<IEnumerable<Transaction>> GetAllForExportAsync(
        TransactionFilterDto filter, CancellationToken ct = default)
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

        return await query
            .OrderByDescending(t => t.CreatedAt)
            .Take(10_000) // safety cap for CSV export
            .ToListAsync(ct);
    }
}
