using WebTemplate.Api.Models.Entities;

namespace WebTemplate.Api.Repositories;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default);
    Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
    void Detach(RefreshToken token);
}
