using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Models.Settings;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Services;

/// <summary>JWT Access Token 與 Refresh Token 的產生、驗證及撤銷服務，實作 <see cref="ITokenService"/>。</summary>
public class TokenService(IRefreshTokenRepository refreshTokenRepository, IOptions<JwtSettings> jwtOptions) : ITokenService
{
    private readonly JwtSettings _jwt = jwtOptions.Value;

    // 改用新版 JsonWebTokenHandler（Microsoft.IdentityModel.JsonWebTokens），
    // 原因：
    // 1. 效能優於舊版 JwtSecurityTokenHandler（避免 XML 派生類別的序列化開銷）。
    // 2. 預設 API 行為較單純，不會隱式把 RFC 7519 標準短名稱（sub/email/role）
    //    映射成 .NET 的 ClaimTypes URI 長字串，避免驗證端需同時處理兩套名稱。
    // 驗證端另設定 MapInboundClaims = false + NameClaimType/RoleClaimType 字串，
    // 讓兩側統一使用 JWT 標準短名稱（sub、name、role）。
    private static readonly JsonWebTokenHandler Handler = new();

    /// <inheritdoc/>
    public string GenerateAccessToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // 使用 JWT 標準短名稱而非 ClaimTypes URI，與 MapInboundClaims=false 驗證設定對齊。
        // Role 以小寫字串 "role" 寫入，避免 JsonWebTokenHandler 把 ClaimTypes.Role 的長 URI
        // 原樣寫進 token 讓 payload 過度膨脹。
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            Expires = DateTime.UtcNow.AddMinutes(_jwt.AccessTokenExpirationMinutes),
            SigningCredentials = credentials,
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim("role", user.Role.ToString()),
            }),
        };

        return Handler.CreateToken(descriptor);
    }

    // 使用密碼學安全的亂數產生器（CSPRNG）而非 Random，
    // 64 bytes 提供 512 bits 的熵，實務上不可暴力猜測。
    /// <inheritdoc/>
    public string GenerateRefreshToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes);
    }

    /// <inheritdoc/>
    public async Task<RefreshToken?> GetActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default)
    {
        var tokenHash = HashToken(rawToken);
        return await refreshTokenRepository.FindActiveByHashAsync(tokenHash, ct);
    }

    /// <inheritdoc/>
    public async Task<RefreshTokenPair> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default)
    {
        var rawToken = GenerateRefreshToken();
        var refreshToken = new RefreshToken
        {
            UserId = userId,
            TokenHash = HashToken(rawToken),
            ExpiresAt = DateTime.UtcNow.AddDays(_jwt.RefreshTokenExpirationDays),
        };

        await refreshTokenRepository.CreateAsync(refreshToken, ct);

        // 原實作 detach + 改寫 TokenHash 欄位回傳明文，讓同一個欄位同時代表
        // 「資料庫裡的雜湊」與「回給 caller 的明文」，非常容易誤用
        // （日誌、mapper、後續 re-attach 都可能把明文誤落回資料庫）。
        // 改用 RefreshTokenPair record 清楚切兩個欄位：entity 維持 hash，RawToken 對外。
        return new RefreshTokenPair(refreshToken, rawToken);
    }

    /// <inheritdoc/>
    public Task RevokeRefreshTokenAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default)
        => refreshTokenRepository.RevokeAsync(token, replacedByToken, ct);

    /// <inheritdoc/>
    public Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
        => refreshTokenRepository.RevokeAllForUserAsync(userId, ct);

    // 以 SHA-256 對 refresh token 單向雜湊後存入資料庫。
    // 即使資料庫洩漏，攻擊者也無法直接使用 hash 值冒充合法 token。
    // 注意：SHA-256 適用於高熵隨機字串（如本 token），但不適合雜湊使用者密碼
    // （密碼應使用 bcrypt/argon2 等慢速演算法）。
    /// <summary>以 SHA-256 對 Token 字串單向雜湊，回傳 Base64 編碼的雜湊值。</summary>
    /// <param name="token">原始明文 Token。</param>
    /// <returns>Base64 編碼的 SHA-256 雜湊字串。</returns>
    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
