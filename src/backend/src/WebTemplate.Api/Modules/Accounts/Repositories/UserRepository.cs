using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Infrastructure.Data;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Repositories;

/// <summary>使用者資料存取實作，實作 <see cref="IUserRepository"/>。</summary>
public class UserRepository(AppDbContext db) : IUserRepository
{
    /// <inheritdoc/>
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Users.FindAsync([id], ct);

    /// <inheritdoc/>
    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    /// <inheritdoc/>
    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users.AnyAsync(u => u.Email == email, ct);

    /// <inheritdoc/>
    public async Task<(int Total, IReadOnlyList<User> Items)> FindPagedAsync(
        int page, int limit, string? search, CancellationToken ct = default)
    {
        var query = db.Users.AsQueryable();

        // Contains 在 EF Core 中轉譯為 SQL LIKE '%search%'，為大小寫不敏感的模糊搜尋。
        // 若資料量大，應考慮加入全文搜尋索引（Full-Text Index）以避免全表掃描。
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Contains(search) || u.DisplayName.Contains(search));

        // Count 在 Skip/Take 之前執行，取得過濾後的總筆數，
        // 讓客戶端能計算總頁數（不受分頁影響）。
        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.CreatedAt)
            // Skip 計算需減 1 是因為 page 從 1 開始；若 page 傳入 0 將產生負數，
            // 呼叫端應在 controller 層強制 page >= 1。
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        return (total, items);
    }

    /// <inheritdoc/>
    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    /// <inheritdoc/>
    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
