using Application.DTOs;
using Application.Interfaces;
using Application.Repositories.Interfaces;
using Application.Services.Implementations;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="AuthService"/>.
/// Covers only paths that fail before JWT generation, so no signing-key configuration is
/// required except for <see cref="RefreshTokenAsync"/>, which validates the access token first.
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<IPasswordHasher> _hasher = new();
    private readonly Mock<IConfiguration> _config = new();
    private readonly IMapper _mapper;

    private readonly AuthService _sut;

    private static readonly Guid PlayerId = Guid.NewGuid();

    private static readonly Player StoredPlayer = new()
    {
        Id = PlayerId,
        Username = "testplayer",
        Email = "player@test.com",
        PasswordHash = "hashed-password",
        FirstName = "Test",
        LastName = "Player",
        Status = AccountStatus.Active,
        RowVersion = [1]
    };

    public AuthServiceTests()
    {
        _uow.Setup(u => u.Players).Returns(_players.Object);
        _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        // RefreshTokenAsync builds TokenValidationParameters unconditionally, so the signing
        // key must always resolve to a non-null value even for tests that never reach a
        // successful validation.
        _config.Setup(c => c["JwtSettings:SecretKey"]).Returns("unit-test-signing-key-at-least-32-chars-long");
        _config.Setup(c => c["JwtSettings:Issuer"]).Returns("test-issuer");
        _config.Setup(c => c["JwtSettings:Audience"]).Returns("test-audience");

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<Application.Mappings.MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _sut = new AuthService(_uow.Object, _hasher.Object, _mapper, _config.Object);
    }

    private static Player ClonePlayer(Player source, AccountStatus? status = null) => new()
    {
        Id = source.Id,
        Username = source.Username,
        Email = source.Email,
        PasswordHash = source.PasswordHash,
        FirstName = source.FirstName,
        LastName = source.LastName,
        Status = status ?? source.Status,
        RowVersion = source.RowVersion
    };

    // ═════════════════════════════════════════════════════════════════════════
    // LOGIN
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginAsync_InvalidPassword_ThrowsUnauthorized()
    {
        // Arrange
        var player = ClonePlayer(StoredPlayer);
        _players.Setup(r => r.GetByEmailAsync(player.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), player.PasswordHash)).Returns(false);

        var dto = new LoginDto { EmailOrUsername = player.Email, Password = "wrong-password" };

        // Act
        var act = () => _sut.LoginAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid credentials*");
    }

    [Fact]
    public async Task LoginAsync_SuspendedAccount_ThrowsUnauthorized()
    {
        // Arrange
        var player = ClonePlayer(StoredPlayer, status: AccountStatus.Suspended);
        _players.Setup(r => r.GetByEmailAsync(player.Email, It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);
        _hasher.Setup(h => h.Verify(It.IsAny<string>(), player.PasswordHash)).Returns(true);

        var dto = new LoginDto { EmailOrUsername = player.Email, Password = "correct-password" };

        // Act
        var act = () => _sut.LoginAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*suspended*");
        _players.Verify(r => r.Update(It.IsAny<Player>()), Times.Never,
            "a rejected login must not update LastLoginAt/refresh token");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // REGISTER
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RegisterAsync_EmailAlreadyExists_Throws()
    {
        // Arrange
        _players.Setup(r => r.EmailExistsAsync("taken@test.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var dto = new RegisterDto
        {
            Email = "taken@test.com",
            Username = "newuser",
            Password = "Password123!",
            ConfirmPassword = "Password123!",
            FirstName = "New",
            LastName = "User"
        };

        // Act
        var act = () => _sut.RegisterAsync(dto);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already registered*");
        _players.Verify(r => r.AddAsync(It.IsAny<Player>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // REFRESH TOKEN
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RefreshTokenAsync_MalformedAccessToken_ThrowsUnauthorized()
    {
        // Arrange — an access token that isn't a well-formed JWT must be rejected
        // before any repository lookup happens.
        var dto = new RefreshTokenDto { Token = "not-a-valid-jwt", RefreshToken = "irrelevant" };

        // Act
        var act = () => _sut.RefreshTokenAsync(dto);

        // Assert
        await act.Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*Invalid access token*");
        _players.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // ACTIVATE ACCOUNT
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ActivateAccountAsync_ExpiredToken_Throws()
    {
        // Arrange
        var player = ClonePlayer(StoredPlayer, status: AccountStatus.PendingVerification);
        player.ActivationToken = "expired-token";
        player.ActivationTokenExpiry = DateTime.UtcNow.AddHours(-1);

        _players.Setup(r => r.FirstOrDefaultAsync(
                It.IsAny<System.Linq.Expressions.Expression<Func<Player, bool>>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(player);

        // Act
        var act = () => _sut.ActivateAccountAsync("expired-token");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*expired*");
        _players.Verify(r => r.Update(It.IsAny<Player>()), Times.Never);
    }
}
