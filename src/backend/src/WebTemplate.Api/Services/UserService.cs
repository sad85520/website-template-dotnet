using WebTemplate.Api.Models.DTOs.Auth;
using WebTemplate.Api.Models.DTOs.Common;
using WebTemplate.Api.Repositories;
using WebTemplate.Api.Services.Interfaces;

namespace WebTemplate.Api.Services;

public class UserService(IUserRepository userRepository) : IUserService
{
    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
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
        var (total, items) = await userRepository.FindPagedAsync(page, limit, search, ct);

        var users = items.Select(u => new UserDto
        {
            Id = u.Id,
            Email = u.Email,
            DisplayName = u.DisplayName,
            Role = u.Role.ToString().ToLowerInvariant(),
            CreatedAt = u.CreatedAt,
        });

        return ApiResponse<IEnumerable<UserDto>>.Paginated(users, new PaginationMeta
        {
            Total = total,
            Page = page,
            Limit = limit,
        });
    }
}
