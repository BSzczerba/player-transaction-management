using Application.DTOs;

namespace Application.Services.Interfaces
{
    public interface ITransactionService
    {
        Task<TransactionDto> CreateDepositAsync(Guid playerId, CreateDepositDto dto, string? ipAddress, CancellationToken ct = default);
        Task<TransactionDto> CreateWithdrawalAsync(Guid playerId, CreateWithdrawalDto dto, string? ipAddress, CancellationToken ct = default);
        Task<TransactionDto> ApproveAsync(Guid transactionId, Guid operatorId, string? notes, CancellationToken ct = default);
        Task<TransactionDto> RejectAsync(Guid transactionId, Guid operatorId, string reason, CancellationToken ct = default);
        Task<IEnumerable<TransactionDto>> GetByPlayerAsync(Guid playerId, CancellationToken ct = default);
        Task<IEnumerable<TransactionDto>> GetPendingAsync(CancellationToken ct = default);
        Task<TransactionDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    }
}
