namespace Application.DTOs;

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
    public IEnumerable<TransactionDto> RecentFlaggedTransactions { get; set; } = Enumerable.Empty<TransactionDto>();
}
