using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implementations;

public class ComplianceService : IComplianceService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly IAuditService _audit;
    private readonly ILogger<ComplianceService> _log;

    public ComplianceService(IUnitOfWork uow, IMapper mapper, IAuditService audit, ILogger<ComplianceService> log)
    {
        _uow = uow;
        _mapper = mapper;
        _audit = audit;
        _log = log;
    }

    public async Task<ComplianceSummaryDto> GetSummaryAsync(CancellationToken ct = default)
    {
        return await _uow.Transactions.GetComplianceSummaryAsync(ct);
    }

    public async Task<PlayerRiskProfileDto> GetPlayerRiskProfileAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        var riskStats = await _uow.Transactions.GetPlayerRiskStatsAsync(playerId, ct);
        var amlRaw = await _uow.Transactions.GetAmlScoreRawAsync(playerId, ct);

        var flaggedFilter = new TransactionFilterDto
        {
            PlayerId = playerId,
            IsFlagged = true,
            Page = 1,
            PageSize = 10
        };
        var (recentFlagged, _) = await _uow.Transactions.GetFilteredAsync(flaggedFilter, ct);

        var profile = new PlayerRiskProfileDto
        {
            PlayerId = player.Id,
            Username = player.Username,
            Status = player.Status.ToString(),
            KycVerified = player.KycVerified,
            TotalTransactions = riskStats.TotalTransactions,
            FlaggedTransactions = riskStats.FlaggedTransactions,
            TotalDeposited = riskStats.TotalDeposited,
            TotalWithdrawn = riskStats.TotalWithdrawn,
            CurrentBalance = player.Balance,
            AccountCreated = player.CreatedAt,
            Transactions24h = amlRaw.Transactions24h,
            Transactions7d = amlRaw.Transactions7d,
            MaxSingleTransactionAmount = amlRaw.MaxSingleTransactionAmount,
            TodayVolume = amlRaw.TodayVolume,
            RecentFlaggedTransactions = _mapper.Map<IEnumerable<TransactionDto>>(recentFlagged)
        };

        var breakdown = ComputeScoreBreakdown(profile, amlRaw);
        profile.ScoreBreakdown = breakdown;
        profile.AmlScore = breakdown.KycPoints + breakdown.FlagRatioPoints + breakdown.Velocity24hPoints
                         + breakdown.Velocity7dPoints + breakdown.HighValuePoints + breakdown.DailyVolumePoints;
        profile.RiskLevel = profile.AmlScore switch
        {
            >= 76 => "Critical",
            >= 51 => "High",
            >= 21 => "Medium",
            _     => "Low"
        };

        return profile;
    }

    private static AmlScoreBreakdownDto ComputeScoreBreakdown(PlayerRiskProfileDto profile, AmlScoreRawDto raw)
    {
        var flagRatio = profile.TotalTransactions > 0
            ? (double)profile.FlaggedTransactions / profile.TotalTransactions
            : 0;

        return new AmlScoreBreakdownDto
        {
            // 0 or 15 — no KYC is a meaningful risk indicator
            KycPoints = profile.KycVerified ? 0 : 15,

            // Up to 25 — fraction of all transactions that were flagged
            FlagRatioPoints = flagRatio switch
            {
                >= 0.50 => 25,
                >= 0.25 => 15,
                >= 0.10 => 8,
                > 0     => 3,
                _       => 0
            },

            // Up to 20 — high velocity in 24h is the primary AML velocity signal
            Velocity24hPoints = raw.Transactions24h switch
            {
                >= 5 => 20,   // at or above the AML threshold
                >= 3 => 12,
                >= 1 => 4,
                _    => 0
            },

            // Up to 10 — longer-term velocity (7-day window)
            Velocity7dPoints = raw.Transactions7d switch
            {
                >= 20 => 10,
                >= 10 => 6,
                >= 5  => 3,
                _     => 0
            },

            // Up to 15 — single large transaction mirrors the >10K AML flag rule
            HighValuePoints = raw.MaxSingleTransactionAmount switch
            {
                > 10_000m => 15,
                > 5_000m  => 8,
                > 1_000m  => 3,
                _         => 0
            },

            // Up to 15 — daily volume mirrors the >20K AML flag rule
            DailyVolumePoints = raw.TodayVolume switch
            {
                > 20_000m => 15,
                > 10_000m => 8,
                > 5_000m  => 3,
                _         => 0
            }
        };
    }

    public async Task<PagedResult<TransactionDto>> GetFlaggedTransactionsAsync(
        int page = 1, int pageSize = 20, CancellationToken ct = default)
    {
        var filter = new TransactionFilterDto
        {
            IsFlagged = true,
            Page = page,
            PageSize = pageSize
        };
        var (items, totalCount) = await _uow.Transactions.GetFilteredAsync(filter, ct);
        var actualPageSize = Math.Clamp(pageSize, 1, 100);

        return new PagedResult<TransactionDto>
        {
            Items = _mapper.Map<IEnumerable<TransactionDto>>(items),
            TotalCount = totalCount,
            Page = page,
            PageSize = actualPageSize
        };
    }

    public async Task<TransactionDto> ClearFlagAsync(Guid transactionId, Guid officerId, string notes, CancellationToken ct = default)
    {
        await _uow.BeginTransactionAsync(ct);
        try
        {
            var transaction = await _uow.Transactions.GetByIdAsync(transactionId, ct)
                ?? throw new InvalidOperationException("Transaction not found.");

            if (!transaction.IsFlagged)
                throw new InvalidOperationException("Transaction is not flagged.");

            transaction.IsFlagged = false;
            transaction.FlagReason = $"[Cleared by compliance officer: {notes}] Original: {transaction.FlagReason}";

            _uow.Transactions.Update(transaction);

            await _audit.LogAsync(officerId, "ClearAmlFlag", "Transaction", transactionId,
                details: $"Flag cleared with notes: {notes}", ct: ct);

            await _uow.CommitTransactionAsync(ct);

            _log.LogInformation("AML flag cleared on transaction {TransactionId} by officer {OfficerId}",
                transactionId, officerId);

            return _mapper.Map<TransactionDto>(transaction);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }
    }
}
