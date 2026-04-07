using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Data;
using WebTemplate.Api.Models.DTOs.Auth;
using WebTemplate.Api.Models.DTOs.Common;
using WebTemplate.Api.Services.Interfaces;

namespace WebTemplate.Api.Services;

public class UserService(AppDbContext db) : IUserService
{
    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await db.Users.FindAsync([id], ct);
        if (user is null) return null;

        return new UserDto
        {
            Id = user.Id,
            Email = user.Email,
            DisplayName = user.DisplayName,
            Role = user.Role.ToString().ToLowerInvariant(),
            CreatedAt = user.CreatedAt,
        };
    }

    public async Task<ApiResponse<IEnumerable<UserDto>>> GetAllAsync(
        int page, int limit, string? search, CancellationToken ct = default)
    {
        var query = db.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(u => u.Email.Contains(search) || u.DisplayName.Contains(search));

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderBy(u => u.CreatedAt)
            .Skip((page - 1) * limit)
            .Take(limit)
            .Select(u => new UserDto
            {
                Id = u.Id,
                Email = u.Email,
                DisplayName = u.DisplayName,
                Role = u.Role.ToString().ToLowerInvariant(),
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);

        return ApiResponse<IEnumerable<UserDto>>.Paginated(users, new PaginationMeta
        {
            Total = total,
            Page = page,
            Limit = limit,
        });
    }
}
