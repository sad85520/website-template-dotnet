namespace WebTemplate.Api.Modules.Accounts.Models.Settings;

/// <summary>JWT 相關設定，對應 <c>appsettings.json</c> 的 <c>Jwt</c> 區段。</summary>
public class JwtSettings
{
    // Secret 必須在生產環境中透過環境變數或 Secret Manager 注入，
    // 不可以有預設值（空字串），AddJwtAuthentication 已在啟動時驗證此規則。
    /// <summary>JWT 簽名密鑰；必須在生產環境中透過環境變數或 Secret Manager 注入，不可留空。</summary>
    public string Secret { get; set; } = string.Empty;

    /// <summary>JWT 的 Issuer（發行者）聲明值。</summary>
    public string Issuer { get; set; } = "WebTemplate";

    /// <summary>JWT 的 Audience（受眾）聲明值。</summary>
    public string Audience { get; set; } = "WebTemplate";

    // Access token 刻意設定為短效（15 分鐘），降低 token 洩漏的風險窗口；
    // 客戶端應在到期前透過 refresh token 換取新的 access token。
    /// <summary>Access Token 有效期（分鐘），預設 15 分鐘。</summary>
    public int AccessTokenExpirationMinutes { get; set; } = 15;

    // RefreshTokenExpirationDays 需與 AuthController.RefreshCookieOptions.MaxAge 保持一致，
    // 否則 cookie 和 token 的有效期會不同步。
    /// <summary>Refresh Token 有效期（天數），預設 7 天；須與 <see cref="Controllers.AuthController"/> 的 cookie MaxAge 保持一致。</summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
