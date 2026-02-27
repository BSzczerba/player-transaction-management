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
        var flagged = (await _uow.Transactions.GetFlaggedTransactionsAsync(ct)).ToList();

        var playerGroups = flagged
            .GroupBy(t => t.PlayerId)
            .Select(g => new FlaggedPlayerSummaryDto
            {
                PlayerId = g.Key,
                Username = g.First().Player?.Username ?? "Unknown",
                FlaggedTransactionCount = g.Count(),
                TotalFlaggedAmount = g.Sum(t => t.Amount),
                LatestFlagReason = g.OrderByDescending(t => t.CreatedAt).First().FlagReason
            })
            .OrderByDescending(p => p.FlaggedTransactionCount)
            .ToList();

        return new ComplianceSummaryDto
        {
            TotalFlaggedTransactions = flagged.Count,
            PendingReviewCount = flagged.Count(t => t.Status == TransactionStatus.Pending),
            TotalFlaggedAmount = flagged.Sum(t => t.Amount),
            FlaggedPlayersCount = playerGroups.Count,
            TopFlaggedPlayers = playerGroups.Take(10)
        };
    }

    public async Task<PlayerRiskProfileDto> GetPlayerRiskProfileAsync(Guid playerId, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        var allTransactions = (await _uow.Transactions.GetByPlayerIdAsync(playerId, ct)).ToList();
        var flaggedTransactions = allTransactions.Where(t => t.IsFlagged).ToList();

        return new PlayerRiskProfileDto
        {
            PlayerId = player.Id,
            Username = player.Username,
            Status = player.Status.ToString(),
            KycVerified = player.KycVerified,
            TotalTransactions = allTransactions.Count,
            FlaggedTransactions = flaggedTransactions.Count,
            TotalDeposited = allTransactions
                .Where(t => t.Type == TransactionType.Deposit && t.Status == TransactionStatus.Completed)
                .Sum(t => t.Amount),
            TotalWithdrawn = allTransactions
                .Where(t => t.Type == TransactionType.Withdrawal && t.Status == TransactionStatus.Completed)
                .Sum(t => t.Amount),
            CurrentBalance = player.Balance,
            AccountCreated = player.CreatedAt,
            RecentFlaggedTransactions = _mapper.Map<IEnumerable<TransactionDto>>(
                flaggedTransactions.OrderByDescending(t => t.CreatedAt).Take(10))
        };
    }

    public async Task<IEnumerable<TransactionDto>> GetFlaggedTransactionsAsync(CancellationToken ct = default)
    {
        var flagged = await _uow.Transactions.GetFlaggedTransactionsAsync(ct);
        return _mapper.Map<IEnumerable<TransactionDto>>(flagged);
    }

    public async Task<TransactionDto> ClearFlagAsync(Guid transactionId, Guid officerId, string notes, CancellationToken ct = default)
    {
        var transaction = await _uow.Transactions.GetByIdAsync(transactionId, ct)
            ?? throw new InvalidOperationException("Transaction not found.");

        if (!transaction.IsFlagged)
            throw new InvalidOperationException("Transaction is not flagged.");

        transaction.IsFlagged = false;
        transaction.FlagReason = $"[Cleared by compliance officer: {notes}] Original: {transaction.FlagReason}";

        _uow.Transactions.Update(transaction);
        await _uow.SaveChangesAsync(ct);

        await _audit.LogAsync(officerId, "ClearAmlFlag", "Transaction", transactionId,
            details: $"Flag cleared with notes: {notes}", ct: ct);

        _log.LogInformation("AML flag cleared on transaction {TransactionId} by officer {OfficerId}",
            transactionId, officerId);

        return _mapper.Map<TransactionDto>(transaction);
    }
}
