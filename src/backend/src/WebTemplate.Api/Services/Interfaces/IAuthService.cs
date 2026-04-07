using WebTemplate.Api.Models.DTOs.Auth;

namespace WebTemplate.Api.Services.Interfaces;

public interface IAuthService
{
    Task<(UserDto User, string AccessToken, string RefreshToken)> LoginAsync(LoginRequest request, CancellationToken ct = default);
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<(string AccessToken, string NewRefreshToken)> RefreshAsync(string refreshToken, CancellationToken ct = default);
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
