using Application.DTOs;
using Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace API.Controllers;

/// <summary>
/// Authentication controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(IAuthService authService, ILogger<AuthController> logger)
    {
        _authService = authService;
        _logger = logger;
    }

    /// <summary>
    /// Register a new user
    /// </summary>
    /// <param name="registerDto">Registration data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication response with JWT token</returns>
    /// <response code="200">Returns authentication token and user data</response>
    /// <response code="400">If registration data is invalid</response>
    [HttpPost("register")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<AuthResponseDto>> Register(
        [FromBody] RegisterDto registerDto,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("User registration attempt for email: {Email}", registerDto.Email);

            var response = await _authService.RegisterAsync(registerDto, cancellationToken);

            _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);

            return Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning("Registration failed: {Message}", ex.Message);
            return BadRequest(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during registration for email: {Email}", registerDto.Email);
            return StatusCode(500, new { message = "An error occurred during registration" });
        }
    }

    /// <summary>
    /// Login with email/username and password
    /// </summary>
    /// <param name="loginDto">Login credentials</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Authentication response with JWT token</returns>
    /// <response code="200">Returns authentication token and user data</response>
    /// <response code="401">If credentials are invalid</response>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> Login(
        [FromBody] LoginDto loginDto,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Login attempt for: {EmailOrUsername}", loginDto.EmailOrUsername);

            var response = await _authService.LoginAsync(loginDto, cancellationToken);

            _logger.LogInformation("User logged in successfully: {EmailOrUsername}", loginDto.EmailOrUsername);

            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Login failed for {EmailOrUsername}: {Message}", loginDto.EmailOrUsername, ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during login for: {EmailOrUsername}", loginDto.EmailOrUsername);
            return StatusCode(500, new { message = "An error occurred during login" });
        }
    }

    /// <summary>
    /// Refresh JWT token
    /// </summary>
    /// <param name="refreshTokenDto">Refresh token data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>New authentication token</returns>
    /// <response code="200">Returns new authentication token</response>
    /// <response code="401">If refresh token is invalid</response>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(AuthResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuthResponseDto>> RefreshToken(
        [FromBody] RefreshTokenDto refreshTokenDto,
        CancellationToken cancellationToken)
    {
        try
        {
            var response = await _authService.RefreshTokenAsync(refreshTokenDto, cancellationToken);
            return Ok(response);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogWarning("Token refresh failed: {Message}", ex.Message);
            return Unauthorized(new { message = ex.Message });
        }
        catch (NotImplementedException)
        {
            return StatusCode(501, new { message = "Refresh token functionality will be implemented in next phase" });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during token refresh");
            return StatusCode(500, new { message = "An error occurred during token refresh" });
        }
    }

    /// <summary>
    /// Get current user information
    /// </summary>
    /// <returns>Current user data</returns>
    /// <response code="200">Returns current user data</response>
    /// <response code="401">If not authenticated</response>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(PlayerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<object> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        var username = User.FindFirst(System.Security.Claims.ClaimTypes.Name)?.Value;
        var role = User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value;

        return Ok(new
        {
            id = userId,
            email = email,
            username = username,
            role = role
        });
    }

    /// <summary>
    /// Validate JWT token
    /// </summary>
    /// <param name="token">JWT token to validate</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    /// <response code="200">Token is valid</response>
    /// <response code="400">Token is invalid</response>
    [HttpPost("validate")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult> ValidateToken(
        [FromBody] string token,
        CancellationToken cancellationToken)
    {
        var isValid = await _authService.ValidateTokenAsync(token, cancellationToken);

        if (isValid)
        {
            return Ok(new { valid = true });
        }

        return BadRequest(new { valid = false });
    }
}
