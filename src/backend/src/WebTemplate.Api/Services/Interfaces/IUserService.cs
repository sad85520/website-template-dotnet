using WebTemplate.Api.Models.DTOs.Auth;
using WebTemplate.Api.Models.DTOs.Common;

namespace WebTemplate.Api.Services.Interfaces;

public interface IUserService
{
    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<ApiResponse<IEnumerable<UserDto>>> GetAllAsync(int page, int limit, string? search, CancellationToken ct = default);
}
