using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using WebTemplate.Api.Common.Exceptions;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Settings;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Controllers;

/// <summary>處理認證相關請求：註冊、登入、Token 刷新與登出。</summary>
[ApiController]
[Route("api/v1/[controller]")]
// class-level 套 "auth" 會讓 refresh/logout 也消耗同一配額桶：
// 正常使用者 refresh 每 15 分鐘一次，但若 access token 頻繁過期或 user 多裝置同時活動，
// 容易誤中限流；logout 更是使用者主動結束 session 的請求，被限流會讓 session
// 清除失敗。改為在 Register/Login 兩個實際需要防暴力破解的 action 各自掛
// [EnableRateLimiting("auth")]，refresh/logout 套 "api" 的寬鬆配額。
[EnableRateLimiting("api")]
public class AuthController(
    IAuthService authService,
    IOptions<JwtSettings> jwtOptions,
    IWebHostEnvironment env) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private readonly JwtSettings _jwt = jwtOptions.Value;

    // HttpOnly：JavaScript 無法讀取此 cookie，防止 XSS 竊取 refresh token。
    // Secure：生產環境強制 HTTPS 傳送，防止中間人攔截；
    //        開發環境放寬為 false，否則 docker compose HTTP 下瀏覽器會拒送 cookie，
    //        refresh 端點永遠 401 讓整條 auth 流程無法在本機測試。
    // SameSite = Strict：跨站請求不帶此 cookie，防止 CSRF 攻擊。
    // MaxAge 從 JwtSettings.RefreshTokenExpirationDays 讀取，確保 cookie 生命週期與 token 同步；
    // 不再寫死於程式碼，避免設定漂移。
    // Domain：預設（null）時 cookie 僅綁在當前主機，無法跨子網域。若前端與 API 分別
    //        部署在不同子網域（例如 www.example.com vs api.example.com），必須在
    //        Jwt:RefreshCookieDomain 設 ".example.com" 才能讓兩邊共用 refresh cookie。
    //        刻意不預設值，避免 template 使用者誤把 cookie 寫到錯誤網域造成 session 洩漏。
    private CookieOptions BuildRefreshCookieOptions()
    {
        var options = new CookieOptions
        {
            HttpOnly = true,
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromDays(_jwt.RefreshTokenExpirationDays),
        };

        if (!string.IsNullOrWhiteSpace(_jwt.RefreshCookieDomain))
            options.Domain = _jwt.RefreshCookieDomain;

        return options;
    }

    private int AccessTokenExpiresInSeconds => _jwt.AccessTokenExpirationMinutes * 60;

    /// <summary>建立新帳號。</summary>
    /// <param name="request">包含 Email、密碼與顯示名稱的註冊資料。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 201 Created，包含新建使用者資料。</returns>
    [HttpPost("register")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, user);
    }

    /// <summary>以 Email 與密碼登入，成功後發行 access token 並以 HttpOnly cookie 設定 refresh token。</summary>
    /// <param name="request">登入憑證。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK，包含 access token 與過期秒數（<see cref="LoginResponse"/>）。</returns>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting("auth")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var (_, accessToken, refreshToken) = await authService.LoginAsync(request, ct);

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildRefreshCookieOptions());

        // ExpiresIn 以秒為單位，從 JwtSettings.AccessTokenExpirationMinutes 推導，
        // 客戶端依此值設定 token 自動更新的計時器。
        var response = new LoginResponse(accessToken, AccessTokenExpiresInSeconds);

        return Ok(response);
    }

    /// <summary>使用 HttpOnly cookie 中的 refresh token 換取新的 access token，並輪換 refresh token。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含新的 access token；cookie 不存在時 401。</returns>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            throw new AppAuthenticationException("Refresh token not found.");

        var (accessToken, newRefreshToken) = await authService.RefreshAsync(refreshToken, ct);

        Response.Cookies.Append(RefreshTokenCookieName, newRefreshToken, BuildRefreshCookieOptions());

        return Ok(new LoginResponse(accessToken, AccessTokenExpiresInSeconds));
    }

    /// <summary>撤銷 refresh token 並刪除客戶端 cookie，登出目前 session。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 204 No Content。</returns>
    [HttpPost("logout")]
    // Authorize 確保只有持有效 access token 的請求才能登出，
    // 防止匿名請求惡意觸發大量 token 撤銷操作。
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(refreshToken, ct);

        // 無論 token 是否有效，一律刪除 cookie，確保客戶端 session 被清除。
        Response.Cookies.Delete(RefreshTokenCookieName);
        return NoContent();
    }
}
