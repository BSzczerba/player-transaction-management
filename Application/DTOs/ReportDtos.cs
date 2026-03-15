namespace Application.DTOs;

public class ReportFilterDto
{
    public DateTime StartDate { get; set; } = DateTime.UtcNow.AddDays(-30);
    public DateTime EndDate { get; set; } = DateTime.UtcNow;

    /// <summary>Grouping granularity: Daily | Weekly | Monthly</summary>
    public string GroupBy { get; set; } = "Daily";
}

public class FinancialSummaryReportDto
{
    public DateTime PeriodStart { get; set; }
    public DateTime PeriodEnd { get; set; }
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public decimal NetFlow { get; set; }
    public int TotalTransactions { get; set; }
    public int CompletedTransactions { get; set; }
    public int PendingTransactions { get; set; }
    public int ProcessingTransactions { get; set; }
    public int RejectedTransactions { get; set; }
    public int FlaggedTransactions { get; set; }
    public decimal AverageTransactionAmount { get; set; }
    public IEnumerable<PeriodBreakdownDto> Breakdown { get; set; } = Enumerable.Empty<PeriodBreakdownDto>();
}

public class PeriodBreakdownDto
{
    public string Period { get; set; } = string.Empty;
    public decimal Deposits { get; set; }
    public decimal Withdrawals { get; set; }
    public int TransactionCount { get; set; }
    public int FlaggedCount { get; set; }
}

/// <summary>Intermediate daily stats — used in the service to aggregate into weekly/monthly buckets.</summary>
public class DailyTransactionStatsDto
{
    public DateTime Date { get; set; }
    public decimal Deposits { get; set; }
    public decimal Withdrawals { get; set; }
    public int TransactionCount { get; set; }
    public int FlaggedCount { get; set; }
}

public class PlayerActivityReportDto
{
    public int TotalPlayers { get; set; }
    public int ActivePlayers { get; set; }
    public int SuspendedPlayers { get; set; }
    public int ClosedPlayers { get; set; }
    public int KycVerifiedPlayers { get; set; }
    public int NewPlayersInPeriod { get; set; }
    public IEnumerable<TopPlayerDto> TopPlayersByVolume { get; set; } = Enumerable.Empty<TopPlayerDto>();
}

public class TopPlayerDto
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal TotalVolume { get; set; }
    public int TransactionCount { get; set; }
    public decimal CurrentBalance { get; set; }
}

public class PaymentMethodStatsDto
{
    public Guid PaymentMethodId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public int TransactionCount { get; set; }
    public decimal TotalVolume { get; set; }
    public decimal AverageAmount { get; set; }
}

public class PaymentMethodReportDto
{
    public IEnumerable<PaymentMethodStatsDto> PaymentMethods { get; set; } = Enumerable.Empty<PaymentMethodStatsDto>();
    public string MostUsedByCount { get; set; } = string.Empty;
    public string HighestVolumeMethod { get; set; } = string.Empty;
    public int TotalTransactions { get; set; }
    public decimal TotalVolume { get; set; }
}

/// <summary>Internal raw aggregation returned from the repository for financial summary.</summary>
public class TransactionSummaryRawDto
{
    public decimal TotalDeposits { get; set; }
    public decimal TotalWithdrawals { get; set; }
    public int CompletedCount { get; set; }
    public int PendingCount { get; set; }
    public int ProcessingCount { get; set; }
    public int RejectedCount { get; set; }
    public int FlaggedCount { get; set; }
    public int TotalCount { get; set; }
}
