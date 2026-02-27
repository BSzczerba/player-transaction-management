using Application.DTOs;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ComplianceOfficer,Administrator")]
[Produces("application/json")]
public class ComplianceController : ControllerBase
{
    private readonly IComplianceService _svc;

    public ComplianceController(IComplianceService svc) => _svc = svc;

    /// <summary>Get AML compliance summary dashboard</summary>
    [HttpGet("summary")]
    [ProducesResponseType(typeof(ComplianceSummaryDto), 200)]
    public async Task<IActionResult> GetSummary(CancellationToken ct)
    {
        var summary = await _svc.GetSummaryAsync(ct);
        return Ok(summary);
    }

    /// <summary>Get all flagged transactions</summary>
    [HttpGet("flagged")]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), 200)]
    public async Task<IActionResult> GetFlagged(CancellationToken ct)
    {
        var flagged = await _svc.GetFlaggedTransactionsAsync(ct);
        return Ok(flagged);
    }

    /// <summary>Get risk profile for a specific player</summary>
    [HttpGet("players/{playerId:guid}/risk")]
    [ProducesResponseType(typeof(PlayerRiskProfileDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetPlayerRisk(Guid playerId, CancellationToken ct)
    {
        var profile = await _svc.GetPlayerRiskProfileAsync(playerId, ct);
        return Ok(profile);
    }

    /// <summary>Clear AML flag on a transaction</summary>
    [HttpPost("flagged/{transactionId:guid}/clear")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> ClearFlag(Guid transactionId, [FromBody] ClearFlagDto dto, CancellationToken ct)
    {
        var officerId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var result = await _svc.ClearFlagAsync(transactionId, officerId, dto.Notes, ct);
        return Ok(result);
    }
}

public class ClearFlagDto
{
    public string Notes { get; set; } = string.Empty;
}
