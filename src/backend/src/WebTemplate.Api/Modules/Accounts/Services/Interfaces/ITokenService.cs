using WebTemplate.Api.Modules.Accounts.Models.Entities;

namespace WebTemplate.Api.Modules.Accounts.Services.Interfaces;

/// <summary>Token 產生與管理服務介面。</summary>
public interface ITokenService
{
    /// <summary>為指定使用者產生帶有 Claims 的 JWT Access Token。</summary>
    /// <param name="user">要為其簽發 Token 的使用者。</param>
    /// <returns>已簽名的 JWT 字串。</returns>
    string GenerateAccessToken(User user);

    /// <summary>依原始 Token 字串查詢目前有效的 Refresh Token 實體。</summary>
    /// <param name="rawToken">客戶端持有的原始 Refresh Token（雜湊前）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>有效的 <see cref="RefreshToken"/> 實體；Token 無效或已過期時回傳 <c>null</c>。</returns>
    Task<RefreshToken?> GetActiveRefreshTokenAsync(string rawToken, CancellationToken ct = default);

    /// <summary>使用 CSPRNG 產生新的 Refresh Token 並以 SHA-256 雜湊後存入資料庫。</summary>
    /// <param name="userId">要關聯此 Token 的使用者 ID。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已建立的 <see cref="RefreshToken"/> 實體（含原始 Token 字串）。</returns>
    Task<RefreshToken> CreateRefreshTokenAsync(Guid userId, CancellationToken ct = default);

    /// <summary>撤銷指定的 Refresh Token，可選擇性記錄替換它的新 Token（用於 Token Rotation 追蹤）。</summary>
    /// <param name="token">要撤銷的 Token 實體。</param>
    /// <param name="replacedByToken">替換此 Token 的新原始 Token 字串。</param>
    /// <param name="ct">取消令牌。</param>
    Task RevokeRefreshTokenAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default);

    /// <summary>撤銷指定使用者持有的所有 Refresh Token（例如強制登出或偵測到重放攻擊）。</summary>
    /// <param name="userId">目標使用者的 ID。</param>
    /// <param name="ct">取消令牌。</param>
    Task RevokeAllUserRefreshTokensAsync(Guid userId, CancellationToken ct = default);
}
