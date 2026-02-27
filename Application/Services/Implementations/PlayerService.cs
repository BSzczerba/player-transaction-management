using Application.DTOs;
using Application.Interfaces;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Enums;
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

    public async Task<PlayerDto> UpdateLimitsAsync(Guid playerId, UpdatePlayerLimitsDto dto, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        player.DailyDepositLimit = dto.DailyDepositLimit;
        player.DailyWithdrawalLimit = dto.DailyWithdrawalLimit;

        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("Player {PlayerId} limits updated: deposit={DepositLimit}, withdrawal={WithdrawalLimit}",
            playerId, dto.DailyDepositLimit, dto.DailyWithdrawalLimit);

        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<PlayerDto> UpdateStatusAsync(Guid playerId, AccountStatus newStatus, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        var oldStatus = player.Status;
        player.Status = newStatus;

        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("Player {PlayerId} status changed from {OldStatus} to {NewStatus}",
            playerId, oldStatus, newStatus);

        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<PlayerDto> UpdateRoleAsync(Guid playerId, UserRole newRole, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        var oldRole = player.Role;
        player.Role = newRole;

        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("Player {PlayerId} role changed from {OldRole} to {NewRole}",
            playerId, oldRole, newRole);

        return _mapper.Map<PlayerDto>(player);
    }

    public async Task<PlayerDto> SetKycVerifiedAsync(Guid playerId, bool verified, CancellationToken ct = default)
    {
        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new InvalidOperationException("Player not found.");

        player.KycVerified = verified;

        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        _log.LogInformation("Player {PlayerId} KYC verification set to {Verified}", playerId, verified);

        return _mapper.Map<PlayerDto>(player);
    }
}