using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Models.Settings;
using WebTemplate.Api.Modules.Accounts.Repositories;
using WebTemplate.Api.Modules.Accounts.Services;
using WebTemplate.Api.Tests.Helpers;

namespace WebTemplate.Api.Tests.Services;

/// <summary>
/// <see cref="TokenService"/> 的單元測試，驗證 JWT 產生、refresh token 雜湊儲存及撤銷後狀態。
/// 使用 InMemory EF Core 資料庫以確保雜湊比對行為真實執行，而非透過 mock 遮蔽。
/// </summary>
public class TokenServiceTests
{
    /// <summary>建立測試用 <see cref="JwtSettings"/> 選項，使用指定的 secret 字串。</summary>
    /// <param name="secret">JWT 簽章 secret，須夠長以通過 HMAC-SHA256 最低要求。</param>
    /// <returns>包裝好的 <see cref="IOptions{JwtSettings}"/>。</returns>
    private static IOptions<JwtSettings> CreateJwtOptions(string secret = "super-secret-key-that-is-long-enough-32bytes")
    {
        var settings = new JwtSettings
        {
            Secret = secret,
            Issuer = "TestIssuer",
            Audience = "TestAudience",
            AccessTokenExpirationMinutes = 15,
            RefreshTokenExpirationDays = 7,
        };
        return Options.Create(settings);
    }

    /// <summary>建立測試用 <see cref="User"/> 實體，Id 使用新 GUID，Role 為 User。</summary>
    /// <returns>帶有完整欄位的 <see cref="User"/> 實體。</returns>
    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "token@example.com",
        DisplayName = "Token User",
        Role = UserRole.User,
        PasswordHash = "hash",
    };

    /// <summary>GenerateAccessToken 應回傳可解析的合法 JWT，包含正確 Issuer、Audience 與未來的到期時間。</summary>
    [Fact]
    public void GenerateAccessToken_ReturnsValidJwt()
    {
        using var db = TestDbContextFactory.Create();
        var options = CreateJwtOptions();
        var refreshTokenRepo = new RefreshTokenRepository(db);
        var service = new TokenService(refreshTokenRepo, options);
        var user = CreateUser();

        var token = service.GenerateAccessToken(user);

        Assert.False(string.IsNullOrWhiteSpace(token));

        var handler = new JwtSecurityTokenHandler();
        Assert.True(handler.CanReadToken(token));

        var jwt = handler.ReadJwtToken(token);
        Assert.Equal(options.Value.Issuer, jwt.Issuer);
        Assert.Equal(options.Value.Audience, jwt.Audiences.First());
        Assert.True(jwt.ValidTo > DateTime.UtcNow);
    }

    /// <summary>CreateRefreshTokenAsync 應將 SHA-256 hash 寫入資料庫，但回傳物件含明文 raw token 以傳給客戶端。</summary>
    [Fact]
    public async Task CreateRefreshTokenAsync_StoresHashNotRawToken()
    {
        using var db = TestDbContextFactory.Create();
        var options = CreateJwtOptions();
        var refreshTokenRepo = new RefreshTokenRepository(db);
        var service = new TokenService(refreshTokenRepo, options);
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var result = await service.CreateRefreshTokenAsync(user.Id);
        var rawToken = result.RawToken;

        Assert.False(string.IsNullOrWhiteSpace(rawToken));
        // Entity.TokenHash 永遠是資料庫格式（hash），不應等於明文。
        Assert.NotEqual(rawToken, result.Entity.TokenHash);

        var stored = await db.RefreshTokens.FindAsync(result.Entity.Id);
        Assert.NotNull(stored);
        Assert.NotEqual(rawToken, stored.TokenHash);

        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        Assert.Equal(expectedHash, stored.TokenHash);
    }

    /// <summary>撤銷後的 token 在 GetActiveRefreshTokenAsync 查詢中應回傳 IsActive=false、IsExpired=true。</summary>
    [Fact]
    public async Task RevokeRefreshTokenAsync_ExpiredToken_IsRejectedByGetActiveRefreshTokenAsync()
    {
        using var db = TestDbContextFactory.Create();
        var options = CreateJwtOptions();
        var refreshTokenRepo = new RefreshTokenRepository(db);
        var service = new TokenService(refreshTokenRepo, options);
        var user = CreateUser();
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var rawToken = "expired-raw-token";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var expiredToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(-1),
            User = user,
        };
        db.RefreshTokens.Add(expiredToken);
        await db.SaveChangesAsync();

        var found = await service.GetActiveRefreshTokenAsync(rawToken);
        Assert.NotNull(found);
        Assert.False(found.IsActive);
        Assert.True(found.IsExpired);
    }
}
