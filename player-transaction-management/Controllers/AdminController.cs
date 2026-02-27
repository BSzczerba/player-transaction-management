using Application.DTOs;
using Application.Services.Interfaces;
using Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Administrator")]
[Produces("application/json")]
public class AdminController : ControllerBase
{
    private readonly IPlayerService _playerSvc;
    private readonly IAuditService _auditSvc;
    private readonly ILogger<AdminController> _log;

    public AdminController(IPlayerService playerSvc, IAuditService auditSvc, ILogger<AdminController> log)
    {
        _playerSvc = playerSvc;
        _auditSvc = auditSvc;
        _log = log;
    }

    /// <summary>Update a player's daily transaction limits</summary>
    [HttpPut("players/{playerId:guid}/limits")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> UpdateLimits(Guid playerId, [FromBody] UpdatePlayerLimitsDto dto, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _playerSvc.UpdateLimitsAsync(playerId, dto, ct);

        await _auditSvc.LogAsync(adminId, "UpdatePlayerLimits", "Player", playerId,
            newValues: $"{{\"dailyDeposit\":{dto.DailyDepositLimit},\"dailyWithdrawal\":{dto.DailyWithdrawalLimit}}}",
            ct: ct);

        return Ok(result);
    }

    /// <summary>Suspend a player account</summary>
    [HttpPost("players/{playerId:guid}/suspend")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    public async Task<IActionResult> SuspendPlayer(Guid playerId, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _playerSvc.UpdateStatusAsync(playerId, AccountStatus.Suspended, ct);

        await _auditSvc.LogAsync(adminId, "SuspendPlayer", "Player", playerId, ct: ct);

        return Ok(result);
    }

    /// <summary>Activate a player account</summary>
    [HttpPost("players/{playerId:guid}/activate")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    public async Task<IActionResult> ActivatePlayer(Guid playerId, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _playerSvc.UpdateStatusAsync(playerId, AccountStatus.Active, ct);

        await _auditSvc.LogAsync(adminId, "ActivatePlayer", "Player", playerId, ct: ct);

        return Ok(result);
    }

    /// <summary>Close a player account</summary>
    [HttpPost("players/{playerId:guid}/close")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    public async Task<IActionResult> ClosePlayer(Guid playerId, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _playerSvc.UpdateStatusAsync(playerId, AccountStatus.Closed, ct);

        await _auditSvc.LogAsync(adminId, "ClosePlayer", "Player", playerId, ct: ct);

        return Ok(result);
    }

    /// <summary>Change a user's role</summary>
    [HttpPut("players/{playerId:guid}/role")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    public async Task<IActionResult> UpdateRole(Guid playerId, [FromBody] UpdateRoleDto dto, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _playerSvc.UpdateRoleAsync(playerId, dto.Role, ct);

        await _auditSvc.LogAsync(adminId, "UpdatePlayerRole", "Player", playerId,
            newValues: $"{{\"role\":\"{dto.Role}\"}}", ct: ct);

        return Ok(result);
    }

    /// <summary>Set KYC verification status for a player</summary>
    [HttpPost("players/{playerId:guid}/kyc")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    public async Task<IActionResult> SetKyc(Guid playerId, [FromBody] SetKycDto dto, CancellationToken ct)
    {
        var adminId = GetCurrentUserId();
        var result = await _playerSvc.SetKycVerifiedAsync(playerId, dto.Verified, ct);

        await _auditSvc.LogAsync(adminId, "SetKycVerification", "Player", playerId,
            newValues: $"{{\"kycVerified\":{dto.Verified.ToString().ToLower()}}}", ct: ct);

        return Ok(result);
    }

    /// <summary>Get audit logs (filtered + paginated)</summary>
    [HttpGet("audit-logs")]
    [ProducesResponseType(typeof(PagedResult<AuditLogDto>), 200)]
    public async Task<IActionResult> GetAuditLogs([FromQuery] AuditLogFilterDto filter, CancellationToken ct)
    {
        var result = await _auditSvc.GetAllAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>Get audit logs for a specific player</summary>
    [HttpGet("players/{playerId:guid}/audit-logs")]
    [ProducesResponseType(typeof(IEnumerable<AuditLogDto>), 200)]
    public async Task<IActionResult> GetPlayerAuditLogs(Guid playerId, CancellationToken ct)
    {
        var logs = await _auditSvc.GetByUserAsync(playerId, ct);
        return Ok(logs);
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

public class UpdateRoleDto
{
    public UserRole Role { get; set; }
}

public class SetKycDto
{
    public bool Verified { get; set; }
}
