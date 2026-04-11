using WebTemplate.Api.Modules.Accounts.Models.DTOs;

namespace WebTemplate.Api.Modules.Accounts.Services.Interfaces;

/// <summary>認證服務介面，封裝登入、註冊、Token 刷新與登出流程。</summary>
public interface IAuthService
{
    /// <summary>驗證使用者憑證並建立新的 Token 對。</summary>
    /// <param name="request">包含電子郵件與密碼的登入請求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已驗證的使用者 DTO、Access Token 與原始 Refresh Token 字串。</returns>
    Task<(UserDto User, string AccessToken, string RefreshToken)> LoginAsync(LoginRequest request, CancellationToken ct = default);

    /// <summary>建立新使用者帳號。</summary>
    /// <param name="request">包含電子郵件、密碼與顯示名稱的註冊請求。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>新建立使用者的 DTO。</returns>
    Task<UserDto> RegisterAsync(RegisterRequest request, CancellationToken ct = default);

    /// <summary>驗證 Refresh Token 並輪轉（Rotate）出新的 Token 對。</summary>
    /// <param name="refreshToken">客戶端持有的原始 Refresh Token 字串。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>新的 Access Token 與新的原始 Refresh Token 字串。</returns>
    Task<(string AccessToken, string NewRefreshToken)> RefreshAsync(string refreshToken, CancellationToken ct = default);

    /// <summary>登出：撤銷指定的 Refresh Token，使其立即失效。</summary>
    /// <param name="refreshToken">要撤銷的原始 Refresh Token 字串。</param>
    /// <param name="ct">取消令牌。</param>
    Task LogoutAsync(string refreshToken, CancellationToken ct = default);
}
