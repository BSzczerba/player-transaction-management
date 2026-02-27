using Application.DTOs;
using Domain.Enums;

namespace Application.Services.Interfaces
{
    public interface IPlayerService
    {
        Task<PlayerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<PlayerDto> UpdateAsync(Guid id, UpdatePlayerDto dto, CancellationToken ct = default);
        Task<IEnumerable<PlayerDto>> GetAllAsync(CancellationToken ct = default);
        Task<PlayerDto> UpdateLimitsAsync(Guid playerId, UpdatePlayerLimitsDto dto, CancellationToken ct = default);
        Task<PlayerDto> UpdateStatusAsync(Guid playerId, AccountStatus newStatus, CancellationToken ct = default);
        Task<PlayerDto> UpdateRoleAsync(Guid playerId, UserRole newRole, CancellationToken ct = default);
        Task<PlayerDto> SetKycVerifiedAsync(Guid playerId, bool verified, CancellationToken ct = default);
    }
}
