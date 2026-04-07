using WebTemplate.Api.Models.Entities;

namespace WebTemplate.Api.Services.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default);
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default);
    Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default);
}
