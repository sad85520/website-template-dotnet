using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using WebTemplate.Api.Models.Entities;
using WebTemplate.Api.Models.Settings;
using WebTemplate.Api.Repositories;
using WebTemplate.Api.Services.Interfaces;

namespace WebTemplate.Api.Services;

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

        // Detach before mutating so the DB record retains the hash;
        // the returned object carries the raw token for the caller to set as a cookie.
        refreshTokenRepository.Detach(refreshToken);
        refreshToken.TokenHash = rawToken;
        return refreshToken;
    }

    public Task RevokeRefreshTokenAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default)
        => refreshTokenRepository.RevokeAsync(token, replacedByToken, ct);

    public Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default)
        => refreshTokenRepository.RevokeAllForUserAsync(userId, ct);

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToBase64String(bytes);
    }
}
