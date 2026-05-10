namespace Application.DTOs;

public class AmlScoreBreakdownDto
{
    /// <summary>Points for missing KYC verification (0 or 15)</summary>
    public int KycPoints { get; set; }
    /// <summary>Points based on flagged-to-total transaction ratio (0–25)</summary>
    public int FlagRatioPoints { get; set; }
    /// <summary>Points based on transaction count in the last 24 hours (0–20)</summary>
    public int Velocity24hPoints { get; set; }
    /// <summary>Points based on transaction count in the last 7 days (0–10)</summary>
    public int Velocity7dPoints { get; set; }
    /// <summary>Points for the highest single transaction amount (0–15)</summary>
    public int HighValuePoints { get; set; }
    /// <summary>Points based on today's total transaction volume (0–15)</summary>
    public int DailyVolumePoints { get; set; }
}

/// <summary>Raw aggregates fetched from DB to compute AML score — not exposed via API.</summary>
public class AmlScoreRawDto
{
    public int Transactions24h { get; set; }
    public int Transactions7d { get; set; }
    public decimal MaxSingleTransactionAmount { get; set; }
    public decimal TodayVolume { get; set; }
}

public class ComplianceSummaryDto
{
    public int TotalFlaggedTransactions { get; set; }
    public int PendingReviewCount { get; set; }
    public decimal TotalFlaggedAmount { get; set; }
    public int FlaggedPlayersCount { get; set; }
    public IEnumerable<FlaggedPlayerSummaryDto> TopFlaggedPlayers { get; set; } = Enumerable.Empty<FlaggedPlayerSummaryDto>();
}

public class FlaggedPlayerSummaryDto
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int FlaggedTransactionCount { get; set; }
    public decimal TotalFlaggedAmount { get; set; }
    public string? LatestFlagReason { get; set; }
}

public class PlayerRiskStatsDto
{
    public int TotalTransactions { get; set; }
    public int FlaggedTransactions { get; set; }
    public decimal TotalDeposited { get; set; }
    public decimal TotalWithdrawn { get; set; }
}

public class PlayerRiskProfileDto
{
    public Guid PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool KycVerified { get; set; }
    public int TotalTransactions { get; set; }
    public int FlaggedTransactions { get; set; }
    public decimal TotalDeposited { get; set; }
    public decimal TotalWithdrawn { get; set; }
    public decimal CurrentBalance { get; set; }
    public DateTime AccountCreated { get; set; }

    // AML Score (0–100)
    public int AmlScore { get; set; }
    /// <summary>Low | Medium | High | Critical</summary>
    public string RiskLevel { get; set; } = string.Empty;
    public AmlScoreBreakdownDto ScoreBreakdown { get; set; } = new();

    // Supporting signal values used to compute the score
    public int Transactions24h { get; set; }
    public int Transactions7d { get; set; }
    public decimal MaxSingleTransactionAmount { get; set; }
    public decimal TodayVolume { get; set; }

    public IEnumerable<TransactionDto> RecentFlaggedTransactions { get; set; } = Enumerable.Empty<TransactionDto>();
}
