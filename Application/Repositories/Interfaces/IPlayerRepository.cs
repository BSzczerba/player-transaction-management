using Domain.Entities;

namespace Application.Repositories.Interfaces;

/// <summary>
/// Player repository interface
/// </summary>
public interface IPlayerRepository : IRepository<Player>
{
    /// <summary>
    /// Get player by email
    /// </summary>
    Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get player by username
    /// </summary>
    Task<Player?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if email exists
    /// </summary>
    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if username exists
    /// </summary>
    Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get players by role
    /// </summary>
    Task<IEnumerable<Player>> GetByRoleAsync(Domain.Enums.UserRole role, CancellationToken cancellationToken = default);

    /// <summary>SQL-level count by account status (avoids loading all entities into memory).</summary>
    Task<int> CountByStatusAsync(Domain.Enums.AccountStatus status, CancellationToken ct = default);

    /// <summary>SQL-level count of KYC-verified players.</summary>
    Task<int> CountKycVerifiedAsync(CancellationToken ct = default);

    /// <summary>SQL-level total player count.</summary>
    Task<int> CountTotalAsync(CancellationToken ct = default);

    /// <summary>SQL-level count of players registered within the given UTC range.</summary>
    Task<int> CountNewInPeriodAsync(DateTime start, DateTime end, CancellationToken ct = default);
}
