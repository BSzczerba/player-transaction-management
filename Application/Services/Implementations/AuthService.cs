using Application.DTOs;
using Application.Interfaces;
using Application.Repositories.Interfaces;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace Application.Services.Implementations;

public class AuthService : IAuthService
{
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _hasher;
    private readonly IMapper _mapper;
    private readonly IConfiguration _config;

    public AuthService(IUnitOfWork uow, IPasswordHasher hasher, IMapper mapper, IConfiguration config)
    {
        _uow = uow;
        _hasher = hasher;
        _mapper = mapper;
        _config = config;
    }

    // ─── Register ────────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> RegisterAsync(RegisterDto dto, CancellationToken ct = default)
    {
        if (await _uow.Players.EmailExistsAsync(dto.Email, ct))
            throw new InvalidOperationException("Email already registered.");

        if (await _uow.Players.UsernameExistsAsync(dto.Username, ct))
            throw new InvalidOperationException("Username already taken.");

        var activationToken = Guid.NewGuid().ToString("N");

        var player = new Player
        {
            Email = dto.Email.ToLowerInvariant(),
            Username = dto.Username,
            PasswordHash = _hasher.Hash(dto.Password),
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            PhoneNumber = dto.PhoneNumber,
            DateOfBirth = dto.DateOfBirth,
            Role = UserRole.Player,
            Status = AccountStatus.PendingVerification,
            ActivationToken = activationToken,
            ActivationTokenExpiry = DateTime.UtcNow.AddHours(24)
        };

        await _uow.BeginTransactionAsync(ct);
        try
        {
            await _uow.Players.AddAsync(player, ct);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        // No refresh token at registration — account requires activation first.
        var response = BuildAuthResponse(player, string.Empty);
        // Return the activation token in the response so the caller can simulate the email flow.
        // In production this would be sent via email and NOT included in the API response.
        response.ActivationToken = activationToken;
        return response;
    }

    // ─── Login ───────────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> LoginAsync(LoginDto dto, CancellationToken ct = default)
    {
        var player = dto.EmailOrUsername.Contains('@')
            ? await _uow.Players.GetByEmailAsync(dto.EmailOrUsername.ToLowerInvariant(), ct)
            : await _uow.Players.GetByUsernameAsync(dto.EmailOrUsername, ct);

        if (player is null || !_hasher.Verify(dto.Password, player.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials.");

        if (player.Status == AccountStatus.Suspended)
            throw new UnauthorizedAccessException("Account is suspended.");

        if (player.Status == AccountStatus.Closed)
            throw new UnauthorizedAccessException("Account is closed.");

        player.LastLoginAt = DateTime.UtcNow;
        var rawRefreshToken = StoreRefreshToken(player);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            _uow.Players.Update(player);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        return BuildAuthResponse(player, rawRefreshToken);
    }

    // ─── RefreshToken ────────────────────────────────────────────────────────
    public async Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto dto, CancellationToken ct = default)
    {
        // Validate JWT structure — allow expired tokens but reject invalid signatures
        ClaimsPrincipal principal;
        try
        {
            principal = new JwtSecurityTokenHandler()
                .ValidateToken(dto.Token, TokenValidationParams(validateLifetime: false), out _);
        }
        catch
        {
            throw new UnauthorizedAccessException("Invalid access token.");
        }

        var userIdValue = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (!Guid.TryParse(userIdValue, out var playerId))
            throw new UnauthorizedAccessException("Invalid token claims.");

        var player = await _uow.Players.GetByIdAsync(playerId, ct)
            ?? throw new UnauthorizedAccessException("Player not found.");

        if (player.RefreshToken != HashToken(dto.RefreshToken) ||
            player.RefreshTokenExpiry is null ||
            player.RefreshTokenExpiry <= DateTime.UtcNow)
        {
            throw new UnauthorizedAccessException("Refresh token is invalid or has expired.");
        }

        var rawRefreshToken = StoreRefreshToken(player);

        await _uow.BeginTransactionAsync(ct);
        try
        {
            _uow.Players.Update(player);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        return BuildAuthResponse(player, rawRefreshToken);
    }

    // ─── ActivateAccount ─────────────────────────────────────────────────────
    public async Task<bool> ActivateAccountAsync(string token, CancellationToken ct = default)
    {
        var player = await _uow.Players.FirstOrDefaultAsync(
            p => p.ActivationToken == token, ct);

        if (player is null)
            throw new InvalidOperationException("Invalid activation token.");

        if (player.ActivationTokenExpiry is null || player.ActivationTokenExpiry <= DateTime.UtcNow)
            throw new InvalidOperationException("Activation token has expired. Please request a new one.");

        if (player.Status == AccountStatus.Active)
            throw new InvalidOperationException("Account is already active.");

        player.Status = AccountStatus.Active;
        player.EmailVerified = true;
        player.ActivationToken = null;
        player.ActivationTokenExpiry = null;

        await _uow.BeginTransactionAsync(ct);
        try
        {
            _uow.Players.Update(player);
            await _uow.CommitTransactionAsync(ct);
        }
        catch
        {
            await _uow.RollbackTransactionAsync(ct);
            throw;
        }

        return true;
    }

    // ─── ValidateToken ────────────────────────────────────────────────────────
    public Task<bool> ValidateTokenAsync(string token, CancellationToken ct = default)
    {
        try
        {
            new JwtSecurityTokenHandler().ValidateToken(token, TokenValidationParams(), out _);
            return Task.FromResult(true);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────
    private AuthResponseDto BuildAuthResponse(Player player, string rawRefreshToken)
    {
        var (token, expires) = GenerateJwt(player);

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = rawRefreshToken,
            ExpiresAt = expires,
            User = _mapper.Map<PlayerDto>(player)
        };
    }

    /// <summary>
    /// Generates a new opaque refresh token, stores its SHA-256 hash in the player entity,
    /// and returns the raw token to be sent to the client.
    /// The raw token is never persisted — only the hash is stored.
    /// </summary>
    private static string StoreRefreshToken(Player player)
    {
        var rawToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
        player.RefreshToken = HashToken(rawToken);
        player.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        return rawToken;
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private (string Token, DateTime Expires) GenerateJwt(Player player)
    {
        var key = Encoding.UTF8.GetBytes(Secret());
        var expiry = DateTime.UtcNow.AddMinutes(ExpiryMinutes());

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, player.Id.ToString()),
            new Claim(ClaimTypes.Email,           player.Email),
            new Claim(ClaimTypes.Name,            player.Username),
            new Claim(ClaimTypes.GivenName,       player.FirstName),
            new Claim(ClaimTypes.Surname,         player.LastName),
            new Claim(ClaimTypes.Role,            player.Role.ToString()),
            new Claim("status",                   player.Status.ToString())
        };

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiry,
            Issuer = _config["JwtSettings:Issuer"],
            Audience = _config["JwtSettings:Audience"],
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var handler = new JwtSecurityTokenHandler();
        return (handler.WriteToken(handler.CreateToken(descriptor)), expiry);
    }

    private TokenValidationParameters TokenValidationParams(bool validateLifetime = true) => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret())),
        ValidateIssuer = true,
        ValidIssuer = _config["JwtSettings:Issuer"],
        ValidateAudience = true,
        ValidAudience = _config["JwtSettings:Audience"],
        ValidateLifetime = validateLifetime,
        ClockSkew = TimeSpan.Zero
    };

    private string Secret() => _config["JwtSettings:SecretKey"]!;
    private int ExpiryMinutes() => int.Parse(_config["JwtSettings:ExpirationMinutes"] ?? "60");
}
