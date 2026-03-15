using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using Domain.Enums;
using System.Text;

namespace Application.Services.Implementations;

public class ReportService : IReportService
{
    private readonly IUnitOfWork _uow;

    public ReportService(IUnitOfWork uow)
    {
        _uow = uow;
    }

    public async Task<FinancialSummaryReportDto> GetFinancialSummaryAsync(
        ReportFilterDto filter, CancellationToken ct = default)
    {
        var (start, end) = NormalizeDateRange(filter);

        var raw = await _uow.Transactions.GetFinancialSummaryRawAsync(start, end, ct);
        var daily = await _uow.Transactions.GetDailyStatsAsync(start, end, ct);

        var breakdown = AggregatePeriod(daily, filter.GroupBy);

        return new FinancialSummaryReportDto
        {
            PeriodStart = start,
            PeriodEnd = end,
            TotalDeposits = raw.TotalDeposits,
            TotalWithdrawals = raw.TotalWithdrawals,
            NetFlow = raw.TotalDeposits - raw.TotalWithdrawals,
            TotalTransactions = raw.TotalCount,
            CompletedTransactions = raw.CompletedCount,
            PendingTransactions = raw.PendingCount,
            ProcessingTransactions = raw.ProcessingCount,
            RejectedTransactions = raw.RejectedCount,
            FlaggedTransactions = raw.FlaggedCount,
            AverageTransactionAmount = raw.TotalCount > 0
                ? (raw.TotalDeposits + raw.TotalWithdrawals) / raw.TotalCount
                : 0m,
            Breakdown = breakdown
        };
    }

    public async Task<PlayerActivityReportDto> GetPlayerActivityReportAsync(
        ReportFilterDto filter, CancellationToken ct = default)
    {
        var (start, end) = NormalizeDateRange(filter);

        var players = (await _uow.Players.GetAllAsync(ct)).ToList();
        var topPlayers = await _uow.Transactions.GetTopPlayersByVolumeAsync(start, end, 10, ct);

        return new PlayerActivityReportDto
        {
            TotalPlayers = players.Count,
            ActivePlayers = players.Count(p => p.Status == AccountStatus.Active),
            SuspendedPlayers = players.Count(p => p.Status == AccountStatus.Suspended),
            ClosedPlayers = players.Count(p => p.Status == AccountStatus.Closed),
            KycVerifiedPlayers = players.Count(p => p.KycVerified),
            NewPlayersInPeriod = players.Count(p => p.CreatedAt >= start && p.CreatedAt <= end),
            TopPlayersByVolume = topPlayers
        };
    }

    public async Task<PaymentMethodReportDto> GetPaymentMethodReportAsync(
        ReportFilterDto filter, CancellationToken ct = default)
    {
        var (start, end) = NormalizeDateRange(filter);

        var stats = (await _uow.Transactions.GetPaymentMethodStatsAsync(start, end, ct)).ToList();

        return new PaymentMethodReportDto
        {
            PaymentMethods = stats,
            MostUsedByCount = stats.OrderByDescending(s => s.TransactionCount).FirstOrDefault()?.Name ?? string.Empty,
            HighestVolumeMethod = stats.OrderByDescending(s => s.TotalVolume).FirstOrDefault()?.Name ?? string.Empty,
            TotalTransactions = stats.Sum(s => s.TransactionCount),
            TotalVolume = stats.Sum(s => s.TotalVolume)
        };
    }

    public async Task<byte[]> ExportTransactionsCsvAsync(
        TransactionFilterDto filter, CancellationToken ct = default)
    {
        var transactions = await _uow.Transactions.GetAllForExportAsync(filter, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id,CreatedAt,PlayerUsername,Type,Amount,Status,PaymentMethod,IsFlagged,FlagReason,BalanceBefore,BalanceAfter,ApprovedBy,ApprovedAt,RejectionReason");

        foreach (var t in transactions)
        {
            sb.AppendLine(string.Join(",",
                t.Id,
                t.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                CsvEscape(t.Player?.Username),
                t.Type,
                t.Amount.ToString("F2"),
                t.Status,
                CsvEscape(t.PaymentMethod?.Name),
                t.IsFlagged,
                CsvEscape(t.FlagReason),
                t.BalanceBefore?.ToString("F2") ?? string.Empty,
                t.BalanceAfter?.ToString("F2") ?? string.Empty,
                CsvEscape(t.ApprovedBy?.Username),
                t.ApprovedAt?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? string.Empty,
                CsvEscape(t.RejectionReason)
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    public async Task<byte[]> ExportPlayersCsvAsync(CancellationToken ct = default)
    {
        var players = await _uow.Players.GetAllAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Id,Username,Email,FirstName,LastName,Role,Status,Balance,DailyDepositLimit,DailyWithdrawalLimit,KycVerified,EmailVerified,CreatedAt,LastLoginAt");

        foreach (var p in players)
        {
            sb.AppendLine(string.Join(",",
                p.Id,
                CsvEscape(p.Username),
                CsvEscape(p.Email),
                CsvEscape(p.FirstName),
                CsvEscape(p.LastName),
                p.Role,
                p.Status,
                p.Balance.ToString("F2"),
                p.DailyDepositLimit.ToString("F2"),
                p.DailyWithdrawalLimit.ToString("F2"),
                p.KycVerified,
                p.EmailVerified,
                p.CreatedAt.ToString("yyyy-MM-ddTHH:mm:ssZ"),
                p.LastLoginAt?.ToString("yyyy-MM-ddTHH:mm:ssZ") ?? string.Empty
            ));
        }

        return Encoding.UTF8.GetBytes(sb.ToString());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static (DateTime Start, DateTime End) NormalizeDateRange(ReportFilterDto filter)
    {
        var start = filter.StartDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(filter.StartDate, DateTimeKind.Utc)
            : filter.StartDate.ToUniversalTime();

        var end = filter.EndDate.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(filter.EndDate, DateTimeKind.Utc)
            : filter.EndDate.ToUniversalTime();

        // Ensure end covers the full day
        if (end.TimeOfDay == TimeSpan.Zero)
            end = end.AddDays(1).AddTicks(-1);

        return (start, end);
    }

    private static IEnumerable<PeriodBreakdownDto> AggregatePeriod(
        IEnumerable<DailyTransactionStatsDto> dailyStats, string groupBy)
    {
        return groupBy.ToUpperInvariant() switch
        {
            "WEEKLY" => dailyStats
                .GroupBy(d => MondayOfWeek(d.Date))
                .Select(g => new PeriodBreakdownDto
                {
                    Period = g.Key.ToString("yyyy-MM-dd"),
                    Deposits = g.Sum(d => d.Deposits),
                    Withdrawals = g.Sum(d => d.Withdrawals),
                    TransactionCount = g.Sum(d => d.TransactionCount),
                    FlaggedCount = g.Sum(d => d.FlaggedCount)
                })
                .OrderBy(p => p.Period),

            "MONTHLY" => dailyStats
                .GroupBy(d => new { d.Date.Year, d.Date.Month })
                .Select(g => new PeriodBreakdownDto
                {
                    Period = $"{g.Key.Year}-{g.Key.Month:D2}",
                    Deposits = g.Sum(d => d.Deposits),
                    Withdrawals = g.Sum(d => d.Withdrawals),
                    TransactionCount = g.Sum(d => d.TransactionCount),
                    FlaggedCount = g.Sum(d => d.FlaggedCount)
                })
                .OrderBy(p => p.Period),

            _ => dailyStats.Select(d => new PeriodBreakdownDto // Daily (default)
            {
                Period = d.Date.ToString("yyyy-MM-dd"),
                Deposits = d.Deposits,
                Withdrawals = d.Withdrawals,
                TransactionCount = d.TransactionCount,
                FlaggedCount = d.FlaggedCount
            })
        };
    }

    private static DateTime MondayOfWeek(DateTime date)
    {
        int daysFromMonday = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-daysFromMonday).Date;
    }

    private static string CsvEscape(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (value.Contains(',') || value.Contains('"') || value.Contains('\n'))
            return $"\"{value.Replace("\"", "\"\"")}\"";

        return value;
    }
}
