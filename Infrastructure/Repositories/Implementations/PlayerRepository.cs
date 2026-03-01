using Domain.Entities;
using Domain.Enums;
using Infrastructure.Data;
using Application.Repositories.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Repositories.Implementations;

/// <summary>
/// Player repository implementation
/// </summary>
public class PlayerRepository : Repository<Player>, IPlayerRepository
{
    public PlayerRepository(ApplicationDbContext context) : base(context)
    {
    }

    // Overridden to avoid tracking the full player list loaded only for display (admin list).
    public override async Task<IEnumerable<Player>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .ToListAsync(cancellationToken);
    }

    public async Task<Player?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        // Tracking required: AuthService may update RefreshToken / LastLoginAt on the returned entity.
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Email == email, cancellationToken);
    }

    public async Task<Player?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        // Tracking required: AuthService may update the returned entity.
        return await _dbSet
            .FirstOrDefaultAsync(p => p.Username == username, cancellationToken);
    }

    public async Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(p => p.Email == email, cancellationToken);
    }

    public async Task<bool> UsernameExistsAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AnyAsync(p => p.Username == username, cancellationToken);
    }

    public async Task<IEnumerable<Player>> GetByRoleAsync(UserRole role, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .AsNoTracking()
            .Where(p => p.Role == role)
            .ToListAsync(cancellationToken);
    }
}
