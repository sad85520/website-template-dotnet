using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Infrastructure.Data;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Repositories;

public class RefreshTokenRepository(AppDbContext db) : IRefreshTokenRepository
{
    // 刻意不在查詢條件中過濾 IsActive，讓呼叫端（AuthService）能區分
    // 「token 不存在」與「token 已被撤銷（可能遭重放攻擊）」兩種情境，
    // 後者需要觸發全面撤銷邏輯。
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

    // 將實體從 EF Core 的變更追蹤中移除，讓 TokenService 得以安全地在記憶體中
    // 修改 TokenHash 欄位（替換為明文），而不觸發後續的資料庫更新。
    public void Detach(RefreshToken token)
        => db.Entry(token).State = EntityState.Detached;
}
