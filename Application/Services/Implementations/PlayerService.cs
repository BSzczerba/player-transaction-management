using Application.DTOs;
using Application.Interfaces;
using Application.Services.Interfaces;
using Application.Repositories.Interfaces;
using AutoMapper;
using Microsoft.Extensions.Logging;

namespace Application.Services.Implementations;

public class PlayerService : IPlayerService
{
    private readonly IUnitOfWork _uow;
    private readonly IMapper _mapper;
    private readonly ILogger<PlayerService> _log;

    public PlayerService(IUnitOfWork uow, IMapper mapper, ILogger<PlayerService> log)
    {
        _uow = uow;
        _mapper = mapper;
        _log = log;
    }

    public async Task<PlayerDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(id, ct);
        return player is null ? null : _mapper.Map<PlayerDto>(player);
    }

    public async Task<PlayerDto> UpdateAsync(Guid id, UpdatePlayerDto dto, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(id, ct)
            ?? throw new InvalidOperationException("Player not found.");

        player.FirstName = dto.FirstName;
        player.LastName = dto.LastName;
        player.PhoneNumber = dto.PhoneNumber;
        player.DateOfBirth = dto.DateOfBirth;

        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("Player {PlayerId} profile updated", id);

        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<IEnumerable<PlayerDto>> GetAllAsync(CancellationToken ct = default)
    {
        var players = await _uow.Players.GetAllAsync(ct);
        return _mapper.Map<IEnumerable<PlayerDto>>(players);
    }
}