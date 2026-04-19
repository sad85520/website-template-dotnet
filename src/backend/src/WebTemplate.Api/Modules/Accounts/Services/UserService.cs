using WebTemplate.Api.Common.Models;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Mappings;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Services;

/// <summary>使用者查詢服務，實作 <see cref="IUserService"/>。</summary>
public class UserService(IUserRepository userRepository) : IUserService
{
    /// <inheritdoc/>
    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await userRepository.FindByIdAsync(id, ct);
        return user?.ToDto();
    }

    /// <inheritdoc/>
    public async Task<PagedResult<UserDto>> GetAllAsync(
        int page, int limit, string? search, CancellationToken ct = default)
    {
        var (total, items) = await userRepository.FindPagedAsync(page, limit, search, ct);

        var users = items.Select(u => u.ToDto()).ToList();

        return new PagedResult<UserDto>(users, total, page, limit);
    }
}
