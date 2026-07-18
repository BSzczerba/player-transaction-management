using Application.DTOs;
using Domain.Entities;

namespace Application.Repositories.Interfaces;

/// <summary>
/// Transaction repository interface
/// </summary>
public interface ITransactionRepository : IRepository<Transaction>
{
    /// <summary>
    /// Get transactions for a specific player
    /// </summary>
    Task<IEnumerable<Transaction>> GetByPlayerIdAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get pending transactions
    /// </summary>
    Task<IEnumerable<Transaction>> GetPendingTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get flagged transactions
    /// </summary>
    Task<IEnumerable<Transaction>> GetFlaggedTransactionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player's transactions for today
    /// </summary>
    Task<IEnumerable<Transaction>> GetTodaysTransactionsByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player's transactions from the last 24 hours (rolling window, translated to SQL)
    /// </summary>
    Task<IEnumerable<Transaction>> GetLast24HoursTransactionsByPlayerAsync(Guid playerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get filtered and paginated transactions
    /// </summary>
    Task<(IEnumerable<Transaction> Items, int TotalCount)> GetFilteredAsync(TransactionFilterDto filter, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get compliance summary with SQL-level aggregation
    /// </summary>
    Task<ComplianceSummaryDto> GetComplianceSummaryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player risk stats with SQL-level aggregation
    /// </summary>
    Task<PlayerRiskStatsDto> GetPlayerRiskStatsAsync(Guid playerId, CancellationToken cancellationToken = default);

    // ── Reports ──────────────────────────────────────────────────────────────

    /// <summary>Aggregate financial totals for a date range.</summary>
    Task<TransactionSummaryRawDto> GetFinancialSummaryRawAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>Daily deposit/withdrawal/flag breakdown for a date range.</summary>
    Task<IEnumerable<DailyTransactionStatsDto>> GetDailyStatsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>Top N players by completed transaction volume in the period.</summary>
    Task<IEnumerable<TopPlayerDto>> GetTopPlayersByVolumeAsync(DateTime startDate, DateTime endDate, int limit, CancellationToken ct = default);

    /// <summary>Per-payment-method transaction count, volume, and average for the period.</summary>
    Task<IEnumerable<PaymentMethodStatsDto>> GetPaymentMethodStatsAsync(DateTime startDate, DateTime endDate, CancellationToken ct = default);

    /// <summary>All transactions matching the filter (no pagination — for CSV export, capped at 10 000 rows).</summary>
    Task<IEnumerable<Transaction>> GetAllForExportAsync(TransactionFilterDto filter, CancellationToken ct = default);

    /// <summary>SQL-level aggregates required to compute a player's AML score.</summary>
    Task<AmlScoreRawDto> GetAmlScoreRawAsync(Guid playerId, CancellationToken ct = default);
}
