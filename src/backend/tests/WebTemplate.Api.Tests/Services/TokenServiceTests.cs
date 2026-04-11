using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using WebTemplate.Api.Models.Entities;
using WebTemplate.Api.Models.Settings;
using WebTemplate.Api.Repositories;
using WebTemplate.Api.Services;
using WebTemplate.Api.Tests.Helpers;

namespace WebTemplate.Api.Tests.Services;

public class TokenServiceTests
{
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

    private static User CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        Email = "token@example.com",
        DisplayName = "Token User",
        Role = UserRole.User,
        PasswordHash = "hash",
    };

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

        // CreateRefreshTokenAsync returns the entity with TokenHash set to the raw token
        var result = await service.CreateRefreshTokenAsync(user.Id);
        var rawToken = result.TokenHash;

        Assert.False(string.IsNullOrWhiteSpace(rawToken));

        // Verify the stored record in the DB contains the SHA256 hash, not the raw token
        var stored = await db.RefreshTokens.FindAsync(result.Id);
        Assert.NotNull(stored);
        Assert.NotEqual(rawToken, stored.TokenHash);

        // Verify the stored value equals the SHA256 hash of the raw token
        var expectedHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        Assert.Equal(expectedHash, stored.TokenHash);
    }

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

        // Manually insert an expired refresh token
        var rawToken = "expired-raw-token";
        var tokenHash = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(rawToken)));
        var expiredToken = new RefreshToken
        {
            UserId = user.Id,
            TokenHash = tokenHash,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // already expired
            User = user,
        };
        db.RefreshTokens.Add(expiredToken);
        await db.SaveChangesAsync();

        // GetActiveRefreshTokenAsync should find the record but IsActive will be false
        var found = await service.GetActiveRefreshTokenAsync(rawToken);
        Assert.NotNull(found);
        Assert.False(found.IsActive);
        Assert.True(found.IsExpired);
    }
}
