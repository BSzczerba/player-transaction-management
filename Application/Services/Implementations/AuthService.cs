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
            Status = AccountStatus.PendingVerification
        };

        await _uow.Players.AddAsync(player, ct);
        await _uow.SaveChangesAsync(ct);

        return BuildAuthResponse(player);
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
        _uow.Players.Update(player);
        await _uow.SaveChangesAsync(ct);

        return BuildAuthResponse(player);
    }

    // ─── RefreshToken (placeholder) ──────────────────────────────────────────
    public Task<AuthResponseDto> RefreshTokenAsync(RefreshTokenDto refreshTokenDto, CancellationToken cancellationToken = default)
    {
        throw new NotSupportedException("Refresh token flow not implemented. Implement persistent refresh tokens before using this method.");
    }

    // ─── ValidateToken ────────────────────────────────────────────────────────
    public Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
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
    private AuthResponseDto BuildAuthResponse(Player player)
    {
        var (token, expires) = GenerateJwt(player);
        var refreshToken = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        return new AuthResponseDto
        {
            Token = token,
            RefreshToken = refreshToken,
            ExpiresAt = expires,
            User = _mapper.Map<PlayerDto>(player)
        };
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

    private TokenValidationParameters TokenValidationParams() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Secret())),
        ValidateIssuer = true,
        ValidIssuer = _config["JwtSettings:Issuer"],
        ValidateAudience = true,
        ValidAudience = _config["JwtSettings:Audience"],
        ValidateLifetime = true,
        ClockSkew = TimeSpan.Zero
    };

    private string Secret() => _config["JwtSettings:SecretKey"]!;
    private int ExpiryMinutes() => int.Parse(_config["JwtSettings:ExpirationMinutes"] ?? "60");
}