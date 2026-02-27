using Application.DTOs;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace API.Controllers;

/// <summary>
/// Authentication controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
[EnableRateLimiting("auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly IPlayerService _playerService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        IAuthService authService,
        IPlayerService playerService,
        ILogger<AuthController> logger)
    {
        _authService = authService;
        _playerService = playerService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new player account.
    /// The response includes an <c>activationToken</c> field — in production this would
    /// be delivered via email. Use it with <c>POST /api/auth/activate</c> to activate the account.
    /// </summary>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterDto registerDto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Registration attempt: {Email}", registerDto.Email);
        var response = await _authService.RegisterAsync(registerDto, cancellationToken);
        _logger.LogInformation("Registered: {Email}", registerDto.Email);
        return Ok(response);
    }

    /// <summary>
    /// Authenticate with email/username and password.
    /// </summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginDto loginDto,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Login attempt: {EmailOrUsername}", loginDto.EmailOrUsername);
        var response = await _authService.LoginAsync(loginDto, cancellationToken);
        _logger.LogInformation("Logged in: {EmailOrUsername}", loginDto.EmailOrUsername);
        return Ok(response);
    }

    /// <summary>
    /// Activate a player account using the token received during registration.
    /// </summary>
    [HttpPost("activate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ActivateAccount(
        [FromBody] ActivateAccountDto dto,
        CancellationToken cancellationToken)
    {
        await _authService.ActivateAccountAsync(dto.Token, cancellationToken);
        return Ok(new { message = "Account activated successfully." });
    }

    /// <summary>
    /// Issue a new JWT using a valid refresh token.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(
        [FromBody] RefreshTokenDto refreshTokenDto,
        CancellationToken cancellationToken)
    {
        var response = await _authService.RefreshTokenAsync(refreshTokenDto, cancellationToken);
        return Ok(response);
    }

    /// <summary>
    /// Get the currently authenticated player's up-to-date profile from the database.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(PlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
        var player = await _playerService.GetByIdAsync(userId, cancellationToken);
        return player is null ? NotFound() : Ok(player);
    }

    /// <summary>
    /// Validate a JWT token.
    /// </summary>
    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ValidateToken(
        [FromBody] string token,
        CancellationToken cancellationToken)
    {
        var isValid = await _authService.ValidateTokenAsync(token, cancellationToken);
        return isValid ? Ok(new { valid = true }) : BadRequest(new { valid = false });
    }
}
