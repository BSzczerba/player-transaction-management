using Application.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Application.Services.Interfaces;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class TransactionsController : ControllerBase
{
    private readonly ITransactionService _svc;
    private readonly ILogger<TransactionsController> _log;

    public TransactionsController(ITransactionService svc, ILogger<TransactionsController> log)
    {
        _svc = svc;
        _log = log;
    }

    /// <summary>Pobierz transakcję po ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tx = await _svc.GetByIdAsync(id, ct);
        return tx is null ? NotFound() : Ok(tx);
    }

    /// <summary>Pobierz transakcje zalogowanego gracza</summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), 200)]
    public async Task<IActionResult> GetMy(CancellationToken ct)
    {
        var playerId = GetCurrentUserId();
        var txs = await _svc.GetByPlayerAsync(playerId, ct);
        return Ok(txs);
    }

    /// <summary>Pobierz wszystkie oczekujące transakcje (tylko Operator/Admin)</summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Operator,Administrator")]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), 200)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var txs = await _svc.GetPendingAsync(ct);
        return Ok(txs);
    }

    /// <summary>Utwórz wpłatę (deposit)</summary>
    [HttpPost("deposit")]
    [ProducesResponseType(typeof(TransactionDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositDto dto, CancellationToken ct)
    {
        try
        {
            var playerId = GetCurrentUserId();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var tx = await _svc.CreateDepositAsync(playerId, dto, ip, ct);

            _log.LogInformation("Deposit created: {TransactionId} for {PlayerId}", tx.Id, playerId);
            return CreatedAtAction(nameof(GetById), new { id = tx.Id }, tx);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Utwórz wypłatę (withdrawal)</summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(TransactionDto), 201)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalDto dto, CancellationToken ct)
    {
        try
        {
            var playerId = GetCurrentUserId();
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
            var tx = await _svc.CreateWithdrawalAsync(playerId, dto, ip, ct);

            _log.LogInformation("Withdrawal created: {TransactionId} for {PlayerId}", tx.Id, playerId);
            return CreatedAtAction(nameof(GetById), new { id = tx.Id }, tx);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Zatwierdź transakcję (tylko Operator/Admin)</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Operator,Administrator")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveDto? dto, CancellationToken ct)
    {
        try
        {
            var operatorId = GetCurrentUserId();
            var tx = await _svc.ApproveAsync(id, operatorId, dto?.Notes, ct);

            _log.LogInformation("Transaction {TransactionId} approved by {OperatorId}", id, operatorId);
            return Ok(tx);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Odrzuć transakcję (tylko Operator/Admin)</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Operator,Administrator")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectDto dto, CancellationToken ct)
    {
        try
        {
            var operatorId = GetCurrentUserId();
            var tx = await _svc.RejectAsync(id, operatorId, dto.Reason, ct);

            _log.LogInformation("Transaction {TransactionId} rejected by {OperatorId}", id, operatorId);
            return Ok(tx);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}

// Helper DTOs (małe, proste - można też dodać do Application/DTOs)
public record ApproveDto(string? Notes);
public record RejectDto(string Reason);