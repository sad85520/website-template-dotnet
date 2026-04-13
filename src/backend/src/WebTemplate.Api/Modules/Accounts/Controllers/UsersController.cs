using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebTemplate.Api.Common.Models;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Controllers;

/// <summary>處理使用者資源的查詢操作，所有端點均需有效 JWT 驗證。</summary>
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class UsersController(IUserService userService) : ControllerBase
{
    /// <summary>取得目前已登入使用者的資料，使用者 ID 從 JWT Claim 解析。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含使用者資料；JWT Claim 無效時 401；找不到使用者時 404。</returns>
    [HttpGet("me")]
    public async Task<IActionResult> GetMe(CancellationToken ct)
    {
        // 從 JWT Claim 中取得使用者 ID，而非從 request body 接受客戶端傳入的 ID，
        // 防止使用者透過竄改參數存取他人資料（IDOR 攻擊）。
        var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(ApiResponse<object>.Fail("Invalid token claims."));

        var user = await userService.GetByIdAsync(userId, ct);

        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        return Ok(ApiResponse<UserDto>.Ok(user));
    }

    // GetAll 及 GetById 僅限 Admin 角色存取；
    // 即使一般使用者持有有效 JWT，授權框架也會在此層拒絕，不會進入 service 層。
    /// <summary>分頁查詢所有使用者，僅限 Admin 角色存取。</summary>
    /// <param name="page">頁碼，從 1 開始，預設 1。</param>
    /// <param name="limit">每頁筆數，預設 20。</param>
    /// <param name="search">可選關鍵字，比對 Email 或顯示名稱。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含分頁使用者列表。</returns>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int limit = 20,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await userService.GetAllAsync(page, limit, search, ct);
        return Ok(result);
    }

    /// <summary>依 ID 查詢特定使用者，僅限 Admin 角色存取。</summary>
    /// <param name="id">使用者 GUID。</param>
    /// <param name="ct">取消權杖。</param>
    /// <returns>HTTP 200 OK 含使用者資料；找不到時 404。</returns>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await userService.GetByIdAsync(id, ct);

        if (user is null)
            return NotFound(ApiResponse<object>.Fail("User not found."));

        return Ok(ApiResponse<UserDto>.Ok(user));
    }
}
