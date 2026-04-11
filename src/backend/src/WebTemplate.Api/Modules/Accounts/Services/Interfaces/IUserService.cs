using WebTemplate.Api.Common.Models;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;

namespace WebTemplate.Api.Modules.Accounts.Services.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<IEnumerable<UserDto>>> GetAllAsync(int page, int limit, string? search, CancellationToken ct = default);
}
