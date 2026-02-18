using Application.DTOs;
using Application.Services.Interfaces;
using Application.Services.Implementations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace API.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
[Produces("application/json")]
public class PlayersController : ControllerBase
{
    private readonly IPlayerService _svc;

    public PlayersController(IPlayerService svc) => _svc = svc;

    /// <summary>Pobierz profil zalogowanego gracza</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var player = await _svc.GetByIdAsync(id, ct);
        return player is null ? NotFound() : Ok(player);
    }

    /// <summary>Aktualizuj profil</summary>
    [HttpPut("me")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    [ProducesResponseType(400)]
    public async Task<IActionResult> UpdateMe([FromBody] UpdatePlayerDto dto, CancellationToken ct)
    {
        try
        {
            var id = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var player = await _svc.UpdateAsync(id, dto, ct);
            return Ok(player);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>Pobierz wszystkich graczy (tylko Admin)</summary>
    [HttpGet]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(IEnumerable<PlayerDto>), 200)]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var players = await _svc.GetAllAsync(ct);
        return Ok(players);
    }

    /// <summary>Pobierz gracza po ID (tylko Admin)</summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    [ProducesResponseType(typeof(PlayerDto), 200)]
    [ProducesResponseType(404)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var player = await _svc.GetByIdAsync(id, ct);
        return player is null ? NotFound() : Ok(player);
    }
}