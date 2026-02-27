using Application.DTOs;

namespace Application.Services.Interfaces;

public interface IComplianceService
{
    Task<ComplianceSummaryDto> GetSummaryAsync(CancellationToken ct = default);
    Task<PlayerRiskProfileDto> GetPlayerRiskProfileAsync(Guid playerId, CancellationToken ct = default);
    Task<IEnumerable<TransactionDto>> GetFlaggedTransactionsAsync(CancellationToken ct = default);
    Task<TransactionDto> ClearFlagAsync(Guid transactionId, Guid officerId, string notes, CancellationToken ct = default);
}
