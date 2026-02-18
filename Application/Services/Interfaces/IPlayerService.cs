using Application.DTOs;

namespace Application.Services.Interfaces
{
    public interface IPlayerService
    {
        Task<PlayerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
        Task<PlayerDto> UpdateAsync(Guid id, UpdatePlayerDto dto, CancellationToken ct = default);
        Task<IEnumerable<PlayerDto>> GetAllAsync(CancellationToken ct = default);
    }
}
