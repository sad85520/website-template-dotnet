using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using WebTemplate.Api.Common.Exceptions;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Controllers;

/// <summary>處理使用者資源的查詢操作，所有端點均需有效 JWT 驗證。</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
// 套用 "api" 策略避免已登入使用者（尤其 Admin 的 GetAll/GetById）被濫用產生
// 全表掃描 DoS；AuthController 已套 "auth" 策略，兩組配額桶互不干擾。
[EnableRateLimiting("api")]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>取得目前已登入使用者的資料，使用者 ID 從 JWT Claim 解析。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含使用者資料；JWT Claim 無效時 401；找不到使用者時 404。</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        // 從 JWT Claim 中取得使用者 ID，而非從 request body 接受客戶端傳入的 ID，
        // 防止使用者透過竄改參數存取他人資料（IDOR 攻擊）。
        // 改用 JWT 標準 "sub" 短名稱，對齊 MapInboundClaims = false 的全域設定。
        var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            throw new AppAuthenticationException("Invalid token claims.");

        var user = await userService.GetByIdAsync(userId, ct)
            ?? throw new AppNotFoundException("User not found.");

        return Ok(user);
    }

    // GetAll 及 GetById 僅限 Admin 角色存取；
    // 即使一般使用者持有有效 JWT，授權框架也會在此層拒絕，不會進入 service 層。
    /// <summary>分頁查詢所有使用者，僅限 Admin 角色存取。分頁中繼資料透過 <c>X-Pagination-*</c> 標頭回傳。</summary>
    /// <param name="page">頁碼，從 1 開始，預設 1。</param>
    /// <param name="limit">每頁筆數，預設 20，最多 100。</param>
    /// <param name="search">可選關鍵字，比對 Email 或顯示名稱。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含使用者陣列；分頁資訊在回應標頭。</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAll(
        [FromQuery][Range(1, int.MaxValue)] int page = 1,
        [FromQuery][Range(1, 100)] int limit = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        // [Range] 由 ApiController 自動回傳 400，但為防禦深度再次 clamp，
        // 避免任何繞過 model binding 驗證的呼叫點（例如 service 層直接呼叫）產生過大查詢。
        var safeLimit = Math.Clamp(limit, 1, 100);
        var safePage = Math.Max(page, 1);
        var result = await userService.GetAllAsync(safePage, safeLimit, search, ct);

        // 回應主體改為直接回傳資源陣列（不再有信封可夾帶 meta），分頁中繼資料
        // 改走 X-Pagination-* 回應標頭，是 RFC 7807／HTTP 原生風格下常見的慣例
        // （類比 GitHub API 的 Link header、其他 API 常見的 X-Total-Count）。
        var totalPages = safeLimit > 0 ? (int)Math.Ceiling((double)result.Total / safeLimit) : 0;
        Response.Headers.Append("X-Pagination-Total", result.Total.ToString());
        Response.Headers.Append("X-Pagination-Page", result.Page.ToString());
        Response.Headers.Append("X-Pagination-Limit", result.Limit.ToString());
        Response.Headers.Append("X-Pagination-Total-Pages", totalPages.ToString());

        return Ok(result.Items);
    }

    /// <summary>依 ID 查詢特定使用者，僅限 Admin 角色存取。</summary>
    /// <param name="id">使用者 GUID。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含使用者資料；找不到時 404。</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(id, ct)
            ?? throw new AppNotFoundException("User not found.");

        return Ok(user);
    }
}
