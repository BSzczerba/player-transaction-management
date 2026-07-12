using Application.Repositories.Interfaces;
using Application.Services.Implementations;
using AutoMapper;
using Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace Tests.Services;

/// <summary>
/// Regression test for the documented <see cref="AuditService.LogAsync"/> invariant (CLAUDE.md):
/// it must only stage the <see cref="AuditLog"/> entity — the caller owns the save via
/// <c>CommitTransactionAsync</c>. A stray SaveChanges here would flush the caller's
/// in-flight transaction early.
/// </summary>
public class AuditServiceTests
{
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly Mock<IAuditLogRepository> _auditLogs = new();
    private readonly IMapper _mapper;

    private readonly AuditService _sut;

    public AuditServiceTests()
    {
        _uow.Setup(u => u.AuditLogs).Returns(_auditLogs.Object);

        var mapperConfig = new MapperConfiguration(cfg => cfg.AddProfile<Application.Mappings.MappingProfile>());
        _mapper = mapperConfig.CreateMapper();

        _sut = new AuditService(_uow.Object, _mapper, NullLogger<AuditService>.Instance);
    }

    [Fact]
    public async Task LogAsync_NeverCallsSaveChanges()
    {
        // Arrange
        _auditLogs.Setup(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuditLog l, CancellationToken _) => l);

        // Act
        await _sut.LogAsync(Guid.NewGuid(), "CreateDeposit", "Transaction", Guid.NewGuid());

        // Assert
        _auditLogs.Verify(r => r.AddAsync(It.IsAny<AuditLog>(), It.IsAny<CancellationToken>()), Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
