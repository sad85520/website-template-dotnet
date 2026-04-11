using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebTemplate.Api.Common.Models;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Modules.Accounts.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize]
public class UsersController(IUserService userService) : ControllerBase
{
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
