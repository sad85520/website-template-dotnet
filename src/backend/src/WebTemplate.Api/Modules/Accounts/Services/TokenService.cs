using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Models.Settings;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Services;

public class TokenService(IRefreshTokenRepository refreshTokenRepository, IOptions<JwtSettings> jwtOptions) : ITokenService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Role, user.Role.ToString()),
        };

        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    // 使用密碼學安全的亂數產生器（CSPRNG）而非 Random，
    // 64 bytes 提供 512 bits 的熵，實務上不可暴力猜測。
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(rawToken);
        return await refreshTokenRepository.FindActiveByHashAsync(tokenHash, ct);
    }

    public async Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var rawToken = GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
        };

        await refreshTokenRepository.CreateAsync(refreshToken, ct);

        // 儲存完成後，將 EF Core 追蹤的實體 detach，
        // 再把 TokenHash 欄位替換為明文 rawToken 後回傳。
        // 這樣做的目的是讓呼叫端（AuthService）能取得明文傳給客戶端，
        // 同時確保資料庫中的記錄維持 hash 狀態不被 EF 意外更新。
        refreshTokenRepository.Detach(refreshToken);
        refreshToken.TokenHash = rawToken;
        return refreshToken;
    }

    public Task RevokeRefreshTokenAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default)
        => refreshTokenRepository.RevokeAsync(token, replacedByToken, ct);

    public Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
        => refreshTokenRepository.RevokeAllForUserAsync(userId, ct);

    // 以 SHA-256 對 refresh token 單向雜湊後存入資料庫。
    // 即使資料庫洩漏，攻擊者也無法直接使用 hash 值冒充合法 token。
    // 注意：SHA-256 適用於高熵隨機字串（如本 token），但不適合雜湊使用者密碼
    // （密碼應使用 bcrypt/argon2 等慢速演算法）。
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
