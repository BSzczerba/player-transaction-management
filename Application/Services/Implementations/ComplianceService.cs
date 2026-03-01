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

        var flaggedFilter = new TransactionFilterDto
        {
            PlayerId = playerId,
            IsFlagged = true,
            Page = 1,
            PageSize = 10
        };
        var (recentFlagged, _) = await _uow.Transactions.GetFilteredAsync(flaggedFilter, ct);

        return new PlayerRiskProfileDto
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
            RecentFlaggedTransactions = _mapper.Map<IEnumerable<TransactionDto>>(recentFlagged)
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
