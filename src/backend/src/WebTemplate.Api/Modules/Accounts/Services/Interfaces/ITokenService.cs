using WebTemplate.Api.Modules.Accounts.Models.Entities;

namespace WebTemplate.Api.Modules.Accounts.Services.Interfaces;

public interface ITokenService
{
    string GenerateAccessToken(User user);
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default);
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default);
    Task RevokeRefreshTokenAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default);
    Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default);
}
