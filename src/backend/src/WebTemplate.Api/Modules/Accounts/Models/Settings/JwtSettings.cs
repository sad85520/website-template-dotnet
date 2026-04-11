namespace WebTemplate.Api.Modules.Accounts.Models.Settings;

public class JwtSettings
{
    // Secret 必須在生產環境中透過環境變數或 Secret Manager 注入，
    // 不可以有預設值（空字串），AddJwtAuthentication 已在啟動時驗證此規則。
    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "WebTemplate";
    public string Audience { get; set; } = "WebTemplate";
    // Access token 刻意設定為短效（15 分鐘），降低 token 洩漏的風險窗口；
    // 客戶端應在到期前透過 refresh token 換取新的 access token。
    public int AccessTokenExpirationMinutes { get; set; } = 15;
    // RefreshTokenExpirationDays 需與 AuthController.RefreshCookieOptions.MaxAge 保持一致，
    // 否則 cookie 和 token 的有效期會不同步。
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
