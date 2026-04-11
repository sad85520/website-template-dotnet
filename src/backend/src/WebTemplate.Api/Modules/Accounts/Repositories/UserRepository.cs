using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Infrastructure.Data;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Repositories;

public class UserRepository(AppDbContext db) : IUserRepository
{
    public async Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default)
        => await db.Users.FindAsync([id], ct);

    public async Task<User?> FindByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users.FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default)
        => await db.Users.AnyAsync(u => u.Email == email, ct);

    public async Task<(int Total, IReadOnlyList<User> Items)> FindPagedAsync(
        int page, int limit, string? search, CancellationToken ct = default)
    {
        var query = db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Contains(search) || u.DisplayName.Contains(search));

        var total = await query.CountAsync(ct);
        var items = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .ToListAsync(ct);

        return (total, items);
    }

    public async Task<User> CreateAsync(User user, CancellationToken ct = default)
    {
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        return user;
    }

    public Task SaveChangesAsync(CancellationToken ct = default)
        => db.SaveChangesAsync(ct);
}
