using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Infrastructure.Data;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Repositories;

public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default)
        => await db.RefreshTokens
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.TokenHash == tokenHash, ct);

    public async Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken ct = default)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return token;
    }

    public async Task RevokeAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default)
    {
        token.RevokedAt = DateTime.UtcNow;
        token.ReplacedByToken = replacedByToken;
        await db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await db.RefreshTokens
            .Where(r => r.UserId == userId && r.RevokedAt == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.RevokedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(ct);
    }

    public void Detach(RefreshToken token)
        => db.Entry(token).State = EntityState.Detached;
}
