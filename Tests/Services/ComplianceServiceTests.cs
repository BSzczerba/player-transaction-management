using Application.DTOs;
using Application.Repositories.Interfaces;
using Application.Services.Implementations;
using Application.Services.Interfaces;
using AutoMapper;
using Domain.Entities;
using Domain.Enums;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Unit tests for <see cref="ComplianceService"/>, focused on the AML score breakdown
/// (<c>GetPlayerRiskProfileAsync</c>) since it is the most complex, previously-untested
/// piece of business logic in the service (6 weighted signals feeding a 0-100 score).
/// </summary>
public class ComplianceServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IPlayerRepository> _players = new();
    private readonly Mock<ITransactionRepository> _transactions = new();
    private readonly Mock<IAuditService> _audit = new();
    private readonly IMapper _mapper;

    private readonly ComplianceService _sut;

    private static readonly Guid PlayerId = Guid.NewGuid();

    public ComplianceServiceTests()
    {
        _uow.Setup(u => u.Players).Returns(_players.Object);
        _uow.Setup(u => u.Transactions).Returns(_transactions.Object);
        _uow.Setup(u => u.BeginTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.CommitTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _uow.Setup(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<Application.Mappings.MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _sut = new ComplianceService(_uow.Object, _mapper, _audit.Object, NullLogger<ComplianceService>.Instance);
    }

    private static Player MakePlayer(bool kycVerified) => new()
    {
        Id = PlayerId,
        Username = "riskplayer",
        Email = "risk@test.com",
        Status = AccountStatus.Active,
        KycVerified = kycVerified,
        Balance = 1_000m,
        RowVersion = [1]
    };

    // ═════════════════════════════════════════════════════════════════════════
    // AML SCORE BREAKDOWN
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetPlayerRiskProfileAsync_NoKycAndNoActivity_ComputesLowRiskScore()
    {
        // Arrange — only the KYC signal contributes (15 pts); everything else is zero activity.
        var player = MakePlayer(kycVerified: false);
        _players.Setup(r => r.GetByIdAsync(PlayerId, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        _transactions.Setup(r => r.GetPlayerRiskStatsAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerRiskStatsDto
            {
                TotalTransactions = 0,
                FlaggedTransactions = 0,
                TotalDeposited = 0,
                TotalWithdrawn = 0
            });
        _transactions.Setup(r => r.GetAmlScoreRawAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AmlScoreRawDto
            {
                Transactions24h = 0,
                Transactions7d = 0,
                MaxSingleTransactionAmount = 0m,
                TodayVolume = 0m
            });
        _transactions.Setup(r => r.GetFilteredAsync(It.IsAny<TransactionFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Transaction>(), 0));

        // Act
        var result = await _sut.GetPlayerRiskProfileAsync(PlayerId);

        // Assert
        result.AmlScore.Should().Be(15);
        result.RiskLevel.Should().Be("Low");
        result.ScoreBreakdown.KycPoints.Should().Be(15);
    }

    [Fact]
    public async Task GetPlayerRiskProfileAsync_HighVelocityHighValueAndFlagRatio_ComputesCriticalRiskScore()
    {
        // Arrange — KYC verified (0 pts), but every other signal maxes out:
        // flag ratio 60% (25) + 24h velocity >=5 (20) + 7d velocity >=20 (10)
        // + single amount >10k (15) + daily volume >20k (15) = 85 -> Critical.
        var player = MakePlayer(kycVerified: true);
        _players.Setup(r => r.GetByIdAsync(PlayerId, It.IsAny<CancellationToken>())).ReturnsAsync(player);

        _transactions.Setup(r => r.GetPlayerRiskStatsAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PlayerRiskStatsDto
            {
                TotalTransactions = 10,
                FlaggedTransactions = 6,
                TotalDeposited = 50_000m,
                TotalWithdrawn = 10_000m
            });
        _transactions.Setup(r => r.GetAmlScoreRawAsync(PlayerId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AmlScoreRawDto
            {
                Transactions24h = 5,
                Transactions7d = 20,
                MaxSingleTransactionAmount = 15_000m,
                TodayVolume = 25_000m
            });
        _transactions.Setup(r => r.GetFilteredAsync(It.IsAny<TransactionFilterDto>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Empty<Transaction>(), 0));

        // Act
        var result = await _sut.GetPlayerRiskProfileAsync(PlayerId);

        // Assert
        result.AmlScore.Should().Be(85);
        result.RiskLevel.Should().Be("Critical");
    }

    // ═════════════════════════════════════════════════════════════════════════
    // CLEAR FLAG
    // ═════════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ClearFlagAsync_TransactionNotFlagged_Throws()
    {
        // Arrange
        var transaction = new Transaction
        {
            Id = Guid.NewGuid(),
            PlayerId = PlayerId,
            Type = TransactionType.Deposit,
            Amount = 100m,
            Status = TransactionStatus.Completed,
            IsFlagged = false,
            RowVersion = [1]
        };
        _transactions.Setup(r => r.GetByIdAsync(transaction.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction);

        // Act
        var act = () => _sut.ClearFlagAsync(transaction.Id, Guid.NewGuid(), "no longer suspicious");

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not flagged*");
        _uow.Verify(u => u.RollbackTransactionAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
