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

    /// <summary>Get a transaction by ID</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var tx = await _svc.GetByIdAsync(id, ct);
        return tx is null ? NotFound() : Ok(tx);
    }

    /// <summary>
    /// Get paginated transactions of the currently logged-in player.
    /// </summary>
    [HttpGet("my")]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), 200)]
    public async Task<IActionResult> GetMy(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken ct = default)
    {
        var playerId = GetCurrentUserId();
        var result = await _svc.GetByPlayerPagedAsync(playerId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>
    /// Get all pending transactions (Operator/Admin only).
    /// </summary>
    [HttpGet("pending")]
    [Authorize(Roles = "Operator,Administrator")]
    [ProducesResponseType(typeof(IEnumerable<TransactionDto>), 200)]
    public async Task<IActionResult> GetPending(CancellationToken ct)
    {
        var txs = await _svc.GetPendingAsync(ct);
        return Ok(txs);
    }

    /// <summary>
    /// Get filtered and paginated transactions (Operator/Admin/ComplianceOfficer only).
    /// All query parameters are optional.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Operator,Administrator,ComplianceOfficer")]
    [ProducesResponseType(typeof(PagedResult<TransactionDto>), 200)]
    public async Task<IActionResult> GetAll(
        [FromQuery] TransactionFilterDto filter,
        CancellationToken ct)
    {
        var result = await _svc.GetAllAsync(filter, ct);
        return Ok(result);
    }

    /// <summary>Create a deposit</summary>
    [HttpPost("deposit")]
    [ProducesResponseType(typeof(TransactionDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> CreateDeposit([FromBody] CreateDepositDto dto, CancellationToken ct)
    {
        var playerId = GetCurrentUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var tx = await _svc.CreateDepositAsync(playerId, dto, ip, ct);

        _log.LogInformation("Deposit created: {TransactionId} for {PlayerId}", tx.Id, playerId);
        return CreatedAtAction(nameof(GetById), new { id = tx.Id }, tx);
    }

    /// <summary>Create a withdrawal</summary>
    [HttpPost("withdraw")]
    [ProducesResponseType(typeof(TransactionDto), 201)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> CreateWithdrawal([FromBody] CreateWithdrawalDto dto, CancellationToken ct)
    {
        var playerId = GetCurrentUserId();
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var tx = await _svc.CreateWithdrawalAsync(playerId, dto, ip, ct);

        _log.LogInformation("Withdrawal created: {TransactionId} for {PlayerId}", tx.Id, playerId);
        return CreatedAtAction(nameof(GetById), new { id = tx.Id }, tx);
    }

    /// <summary>Approve a transaction (Operator/Admin only)</summary>
    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Operator,Administrator")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Approve(Guid id, [FromBody] ApproveTransactionDto? dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        var tx = await _svc.ApproveAsync(id, operatorId, dto?.Notes, ct);

        _log.LogInformation("Transaction {TransactionId} approved by {OperatorId}", id, operatorId);
        return Ok(tx);
    }

    /// <summary>Reject a transaction (Operator/Admin only)</summary>
    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Operator,Administrator")]
    [ProducesResponseType(typeof(TransactionDto), 200)]
    [ProducesResponseType(typeof(ProblemDetails), 400)]
    public async Task<IActionResult> Reject(Guid id, [FromBody] RejectTransactionDto dto, CancellationToken ct)
    {
        var operatorId = GetCurrentUserId();
        var tx = await _svc.RejectAsync(id, operatorId, dto.Reason, ct);

        _log.LogInformation("Transaction {TransactionId} rejected by {OperatorId}", id, operatorId);
        return Ok(tx);
    }

    private Guid GetCurrentUserId()
        => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
}
