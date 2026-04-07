using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using WebTemplate.Api.Models.DTOs.Auth;
using WebTemplate.Api.Models.DTOs.Common;
using WebTemplate.Api.Services.Interfaces;

namespace WebTemplate.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("auth")]
public class AuthController(IAuthService authService) : ControllerBase
{
    private const string RefreshTokenCookieName = "refreshToken";
    private static readonly CookieOptions RefreshCookieOptions = new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Strict,
        MaxAge = TimeSpan.FromDays(7),
    };

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken ct)
    {
        var user = await authService.RegisterAsync(request, ct);
        return StatusCode(StatusCodes.Status201Created, ApiResponse<UserDto>.Created(user));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var (user, accessToken, refreshToken) = await authService.LoginAsync(request, ct);

        Response.Cookies.Append(RefreshTokenCookieName, refreshToken, RefreshCookieOptions);

        var response = new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresIn = 15 * 60, // 15 minutes in seconds
        };

        return Ok(ApiResponse<LoginResponse>.Ok(response));
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (string.IsNullOrEmpty(refreshToken))
            return Unauthorized(ApiResponse<object>.Fail("Refresh token not found."));

        var (accessToken, newRefreshToken) = await authService.RefreshAsync(refreshToken, ct);

        Response.Cookies.Append(RefreshTokenCookieName, newRefreshToken, RefreshCookieOptions);

        return Ok(ApiResponse<LoginResponse>.Ok(new LoginResponse
        {
            AccessToken = accessToken,
            ExpiresIn = 15 * 60,
        }));
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        var refreshToken = Request.Cookies[RefreshTokenCookieName];
        if (!string.IsNullOrEmpty(refreshToken))
            await authService.LogoutAsync(refreshToken, ct);

        Response.Cookies.Delete(RefreshTokenCookieName);
        return Ok(ApiResponse<object>.Ok(null!));
    }
}
