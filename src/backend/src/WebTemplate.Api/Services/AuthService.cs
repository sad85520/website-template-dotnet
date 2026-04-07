using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Data;
using WebTemplate.Api.Models.DTOs.Auth;
using WebTemplate.Api.Models.Entities;
using WebTemplate.Api.Services.Interfaces;

namespace WebTemplate.Api.Services;

public class AuthService(AppDbContext db, ITokenService tokenService) : IAuthService
{
    public async Task<(UserDto User, string AccessToken, string RefreshToken)> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == request.Email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshTokenEntity = await tokenService.CreateRefreshTokenAsync(user.Id, ct);

        return (MapToDto(user), accessToken, refreshTokenEntity.TokenHash);
    }

    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var exists = await db.Users.AnyAsync(u => u.Email == request.Email, ct);
        if (exists)
            throw new InvalidOperationException("Email is already registered.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
        };

        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return MapToDto(user);
    }

    public async Task<(string AccessToken, string NewRefreshToken)> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var tokenEntity = await tokenService.GetActiveRefreshTokenAsync(refreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        if (!tokenEntity.IsActive)
        {
            // 偵測到已撤銷的 token 被重複使用：撤銷整條 token chain
            await tokenService.RevokeAllUserRefreshTokensAsync(tokenEntity.UserId, ct);
            throw new UnauthorizedAccessException("Refresh token has been reused. All sessions revoked.");
        }

        var newRefreshTokenEntity = await tokenService.CreateRefreshTokenAsync(tokenEntity.UserId, ct);
        await tokenService.RevokeRefreshTokenAsync(tokenEntity, newRefreshTokenEntity.TokenHash, ct);

        var accessToken = tokenService.GenerateAccessToken(tokenEntity.User);

        return (accessToken, newRefreshTokenEntity.TokenHash);
    }

    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenEntity = await tokenService.GetActiveRefreshTokenAsync(refreshToken, ct);
        if (tokenEntity?.IsActive == true)
            await tokenService.RevokeRefreshTokenAsync(tokenEntity, ct: ct);
    }

    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString().ToLowerInvariant(),
        CreatedAt = user.CreatedAt,
    };
}
