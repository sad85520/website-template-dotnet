using WebTemplate.Api.Modules.Accounts.Models.Entities;

namespace WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;

/// <summary>Refresh Token 資料存取介面。</summary>
public interface IRefreshTokenRepository
{
    /// <summary>依 Token Hash 查詢目前有效（未撤銷、未過期）的 Refresh Token。</summary>
    /// <param name="tokenHash">SHA-256 雜湊後的 Token 字串。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>找到則回傳對應的 <see cref="RefreshToken"/>；否則回傳 <c>null</c>。</returns>
    Task<RefreshToken?> FindActiveByHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>將新的 Refresh Token 存入資料庫。</summary>
    /// <param name="token">要建立的 Refresh Token 實體。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已儲存的 <see cref="RefreshToken"/> 實體。</returns>
    Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>撤銷指定的 Refresh Token，並可選擇性記錄替換它的新 Token。</summary>
    /// <param name="token">要撤銷的 Token 實體。</param>
    /// <param name="replacedByToken">替換此 Token 的新原始 Token 字串（用於 Token Rotation 追蹤）。</param>
    /// <param name="ct">取消令牌。</param>
    Task RevokeAsync(RefreshToken token, string? replacedByToken = null, CancellationToken ct = default);

    /// <summary>撤銷指定使用者的所有 Refresh Token（例如強制登出或帳號安全事件）。</summary>
    /// <param name="userId">目標使用者的 ID。</param>
    /// <param name="ct">取消令牌。</param>
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);

    /// <summary>將指定 Token 從 EF Core 變更追蹤器中分離，避免後續操作產生意外的狀態污染。</summary>
    /// <param name="token">要分離的 Token 實體。</param>
    void Detach(RefreshToken token);
}
