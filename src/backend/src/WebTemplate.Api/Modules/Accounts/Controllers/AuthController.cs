using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using WebTemplate.Api.Common.Models;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Settings;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Controllers;

/// <summary>處理認證相關請求：註冊、登入、Token 刷新與登出。</summary>
[ApiController]
[Route("api/v1/[controller]")]
[EnableRateLimiting("auth")]
public class AuthController(
    IAuthService authService,
    IOptions<JwtSettings> jwtOptions) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private readonly JwtSettings _jwt = jwtOptions.Value;

    // HttpOnly：JavaScript 無法讀取此 cookie，防止 XSS 竊取 refresh token。
    // Secure：只透過 HTTPS 傳送，防止中間人攔截。
    // SameSite = Strict：跨站請求不帶此 cookie，防止 CSRF 攻擊。
    // MaxAge 從 JwtSettings.RefreshTokenExpirationDays 讀取，確保 cookie 生命週期與 token 同步；
    // 不再寫死於程式碼，避免設定漂移。
    private CookieOptions BuildRefreshCookieOptions() => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        MaxAge = TimeSpan.FromDays(_jwt.RefreshTokenExpirationDays),
    };

    private int AccessTokenExpiresInSeconds => _jwt.AccessTokenExpirationMinutes * 60;

    /// <summary>建立新帳號。</summary>
    /// <param name="request">包含 Email、密碼與顯示名稱的註冊資料。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 201 Created，包含新建使用者資料。</returns>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<UserDto>.Created(user));
    }

    /// <summary>以 Email 與密碼登入，成功後發行 access token 並以 HttpOnly cookie 設定 refresh token。</summary>
    /// <param name="request">登入憑證。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK，包含 access token 與過期秒數（<see cref="LoginResponse"/>）。</returns>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var (user, accessToken, refreshToken) = await authService.LoginAsync(request, ct);

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, BuildRefreshCookieOptions());

        // ExpiresIn 以秒為單位，從 JwtSettings.AccessTokenExpirationMinutes 推導，
        // 客戶端依此值設定 token 自動更新的計時器。
        var response = new LoginResponse(accessToken, AccessTokenExpiresInSeconds);

        return Ok(ApiResponse<LoginResponse>.Ok(response));
    }

    /// <summary>使用 HttpOnly cookie 中的 refresh token 換取新的 access token，並輪換 refresh token。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含新的 access token；cookie 不存在時回傳 401。</returns>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ApiResponse<object>.Fail("Refresh token not found."));

        var (accessToken, newRefreshToken) = await authService.RefreshAsync(refreshToken, ct);

        Response.Cookies.Append(RefreshTokenCookieName, newRefreshToken, BuildRefreshCookieOptions());

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse(accessToken, AccessTokenExpiresInSeconds)));
    }

    /// <summary>撤銷 refresh token 並刪除客戶端 cookie，登出目前 session。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK。</returns>
    [HttpPost("logout")]
    // Authorize 確保只有持有效 access token 的請求才能登出，
    // 防止匿名請求惡意觸發大量 token 撤銷操作。
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(refreshToken, ct);

        // 無論 token 是否有效，一律刪除 cookie，確保客戶端 session 被清除。
        Response.Cookies.Delete(RefreshTokenCookieName);
        return Ok(ApiResponse<object>.Ok(null!));
    }
}
