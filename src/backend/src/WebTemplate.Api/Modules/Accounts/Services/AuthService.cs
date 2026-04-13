using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Services;

/// <summary>認證服務實作，處理登入、註冊、Token 刷新與登出邏輯，實作 <see cref="IAuthService"/>。</summary>
public class AuthService(IUserRepository userRepository, ITokenService tokenService) : IAuthService
{
    /// <inheritdoc/>
    public async Task<(UserDto User, string AccessToken, string RefreshToken)> LoginAsync(
        LoginRequest request, CancellationToken ct = default)
    {
        // 找不到使用者時回傳與密碼錯誤相同的訊息，
        // 防止攻擊者透過不同錯誤訊息枚舉（enumerate）已存在的帳號。
        var user = await userRepository.FindByEmailAsync(request.Email, ct)
            ?? throw new UnauthorizedAccessException("Invalid email or password.");

        if (user.LockoutUntil > DateTime.UtcNow)
            throw new UnauthorizedAccessException("Account is temporarily locked. Try again later.");

        // 密碼驗證失敗時遞增計數並在達到閾值時鎖定帳號，
        // 實作軟性帳號鎖定（soft lockout）以對抗暴力破解。
        // 注意：此處的計數與鎖定邏輯非原子操作，高並發場景下可能略有偏差，
        // 但對於登入嘗試保護已足夠，嚴格場景可改用 Redis 原子計數器。
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            user.FailedLoginAttempts++;
            if (user.FailedLoginAttempts >= 5)
                user.LockoutUntil = DateTime.UtcNow.AddMinutes(15);
            await userRepository.SaveChangesAsync(ct);
            throw new UnauthorizedAccessException("Invalid email or password.");
        }

        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        await userRepository.SaveChangesAsync(ct);

        var accessToken = tokenService.GenerateAccessToken(user);
        var refreshTokenEntity = await tokenService.CreateRefreshTokenAsync(user.Id, ct);

        // CreateRefreshTokenAsync 回傳的實體，其 TokenHash 欄位已被替換為原始（明文）token，
        // 此為刻意設計：service 層對外暴露明文，資料庫中只存 hash。
        return (MapToDto(user), accessToken, refreshTokenEntity.TokenHash);
    }

    /// <inheritdoc/>
    public async Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var exists = await userRepository.ExistsByEmailAsync(request.Email, ct);
        if (exists)
            throw new InvalidOperationException("Email is already registered.");

        var user = new User
        {
            Email = request.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
        };

        await userRepository.CreateAsync(user, ct);

        return MapToDto(user);
    }

    /// <inheritdoc/>
    public async Task<(string AccessToken, string NewRefreshToken)> RefreshAsync(
        string refreshToken, CancellationToken ct = default)
    {
        var tokenEntity = await tokenService.GetActiveRefreshTokenAsync(refreshToken, ct)
            ?? throw new UnauthorizedAccessException("Invalid or expired refresh token.");

        // Refresh Token Rotation 安全機制：
        // FindActiveByHash 僅用 hash 比對，若找到的 token 已被撤銷（IsActive = false），
        // 代表有人持有舊 token 嘗試重用（可能是 token 竊取後的重放攻擊），
        // 此時應撤銷該使用者所有 session 強制重新登入，降低損害範圍。
        if (!tokenEntity.IsActive)
        {
            await tokenService.RevokeAllUserRefreshTokensAsync(tokenEntity.UserId, ct);
            throw new UnauthorizedAccessException("Refresh token has been reused. All sessions revoked.");
        }

        // 先建立新 token，再撤銷舊 token，並將舊 token 的 ReplacedByToken 指向新 token，
        // 保留完整的 token 輪換鏈以便稽核追蹤。
        var newRefreshTokenEntity = await tokenService.CreateRefreshTokenAsync(tokenEntity.UserId, ct);
        await tokenService.RevokeRefreshTokenAsync(tokenEntity, newRefreshTokenEntity.TokenHash, ct);

        var accessToken = tokenService.GenerateAccessToken(tokenEntity.User);

        return (accessToken, newRefreshTokenEntity.TokenHash);
    }

    /// <inheritdoc/>
    public async Task LogoutAsync(string refreshToken, CancellationToken ct = default)
    {
        var tokenEntity = await tokenService.GetActiveRefreshTokenAsync(refreshToken, ct);
        if (tokenEntity?.IsActive == true)
            await tokenService.RevokeRefreshTokenAsync(tokenEntity, ct: ct);
    }

    /// <summary>將 <see cref="User"/> 實體對應至 <see cref="UserDto"/>；Role 轉為小寫字串。</summary>
    /// <param name="user">來源使用者實體。</param>
    /// <returns>對應的 <see cref="UserDto"/>。</returns>
    private static UserDto MapToDto(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        DisplayName = user.DisplayName,
        Role = user.Role.ToString().ToLowerInvariant(),
        CreatedAt = user.CreatedAt,
    };
}
