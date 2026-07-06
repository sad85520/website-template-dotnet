using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;

namespace WebTemplate.Api.Tests.Integration;

/// <summary>
/// <see cref="WebTemplate.Api.Modules.Accounts.Controllers.AuthController"/> 端到端整合測試。
/// 透過 <see cref="CustomWebApplicationFactory"/>（背後為 Testcontainers 啟動的真實 SQL Server）
/// 啟動完整 middleware pipeline（含認證、授權、例外處理、RFC 7807 ProblemDetails 序列化、
/// refresh token cookie 行為），驗證 HTTP 層的契約而非僅 Service 層邏輯。
/// </summary>
[Collection(IntegrationTestCollection.Name)]
[Trait("Category", "Integration")]
public class AuthControllerIntegrationTests : IAsyncLifetime
{
    private readonly CustomWebApplicationFactory _factory;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public AuthControllerIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    /// <summary>每個測試方法執行前，透過 Respawn 清空資料表，確保測試之間互不污染。</summary>
    public Task InitializeAsync() => _factory.ResetDatabaseAsync();

    public Task DisposeAsync() => Task.CompletedTask;

    // ──────────────────────────────────────────────────────────
    // POST /api/v1/auth/register
    // ──────────────────────────────────────────────────────────

    /// <summary>註冊成功時應回傳 201 Created，主體直接是 UserDto（不含信封）。</summary>
    [Fact]
    public async Task Register_ReturnsCreated_WhenValidRequest()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Email = $"register-{Guid.NewGuid():N}@example.com",
            Password = "Passw0rd!Strong",
            DisplayName = "Test User",
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<UserDto>(JsonOpts);
        Assert.NotNull(body);
        Assert.Equal(request.Email, body!.Email);
        Assert.Equal(request.DisplayName, body.DisplayName);
    }

    /// <summary>密碼過短應觸發 DataAnnotations 驗證並回傳框架原生的 400 ValidationProblemDetails。</summary>
    [Fact]
    public async Task Register_ReturnsBadRequest_WhenPasswordTooShort()
    {
        var client = _factory.CreateClient();
        var request = new RegisterRequest
        {
            Email = $"short-{Guid.NewGuid():N}@example.com",
            Password = "short",
            DisplayName = "Test User",
        };

        var response = await client.PostAsJsonAsync("/api/v1/auth/register", request);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(JsonOpts);
        Assert.NotNull(problem);
        Assert.Contains(problem!.Errors, kvp => kvp.Key == nameof(RegisterRequest.Password));
    }

    /// <summary>重複註冊同一 Email 應由 GlobalExceptionHandler 轉譯為 409 ProblemDetails。</summary>
    [Fact]
    public async Task Register_ReturnsConflict_WhenEmailAlreadyExists()
    {
        var client = _factory.CreateClient();
        var email = $"dup-{Guid.NewGuid():N}@example.com";
        var request = new RegisterRequest
        {
            Email = email,
            Password = "Passw0rd!Strong",
            DisplayName = "First User",
        };

        var first = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/v1/auth/register", request);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal("application/problem+json", second.Content.Headers.ContentType?.MediaType);

        var problem = await second.Content.ReadFromJsonAsync<ProblemDetails>(JsonOpts);
        Assert.NotNull(problem);
        Assert.Equal(StatusCodes.Status409Conflict, problem!.Status);
        Assert.Equal("Email is already registered.", problem.Detail);
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/v1/auth/login
    // ──────────────────────────────────────────────────────────

    /// <summary>成功登入應回傳 200、access token（直接為主體，不含信封）、以及 HttpOnly 的 refreshToken cookie。</summary>
    [Fact]
    public async Task Login_ReturnsAccessTokenAndCookie_WhenCredentialsValid()
    {
        var client = _factory.CreateClient();
        var email = $"login-{Guid.NewGuid():N}@example.com";
        const string password = "Passw0rd!Strong";
        await RegisterUserAsync(client, email, password);

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOpts);
        Assert.NotNull(body);
        Assert.False(string.IsNullOrWhiteSpace(body!.AccessToken));
        Assert.Equal(15 * 60, body.ExpiresIn);

        // 驗證 refreshToken cookie 具備安全屬性：HttpOnly、Secure、SameSite=Strict
        Assert.True(response.Headers.TryGetValues("Set-Cookie", out var cookies));
        var refreshCookie = cookies!.FirstOrDefault(c => c.StartsWith("refreshToken=", StringComparison.Ordinal));
        Assert.NotNull(refreshCookie);
        Assert.Contains("httponly", refreshCookie!, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", refreshCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=strict", refreshCookie, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>錯誤密碼應回傳 401 ProblemDetails，且不透露「帳號存在與否」的訊息（防止帳號枚舉）。</summary>
    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenPasswordIncorrect()
    {
        var client = _factory.CreateClient();
        var email = $"badpw-{Guid.NewGuid():N}@example.com";
        await RegisterUserAsync(client, email, "Passw0rd!Strong");

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest { Email = email, Password = "WrongPassword123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>不存在的 Email 登入應與錯誤密碼回傳相同狀態碼（401），保持無法區分以防止帳號枚舉。</summary>
    [Fact]
    public async Task Login_ReturnsUnauthorized_WhenUserNotFound()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/v1/auth/login",
            new LoginRequest
            {
                Email = $"ghost-{Guid.NewGuid():N}@example.com",
                Password = "Passw0rd!Strong",
            });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/v1/auth/refresh
    // ──────────────────────────────────────────────────────────

    /// <summary>未攜帶 refreshToken cookie 時應回傳 401。</summary>
    [Fact]
    public async Task Refresh_ReturnsUnauthorized_WhenCookieMissing()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/refresh", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // POST /api/v1/auth/logout
    // ──────────────────────────────────────────────────────────

    /// <summary>未附 access token 的登出請求應被 <c>[Authorize]</c> 擋下，回傳 401。</summary>
    [Fact]
    public async Task Logout_ReturnsUnauthorized_WhenNoAccessToken()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/v1/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // ──────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────

    /// <summary>輔助方法：呼叫 Register 端點建立使用者，斷言成功後回傳。</summary>
    /// <param name="client">測試用 <see cref="HttpClient"/>。</param>
    /// <param name="email">要建立的帳號 Email。</param>
    /// <param name="password">帳號密碼。</param>
    private static async Task RegisterUserAsync(HttpClient client, string email, string password)
    {
        var response = await client.PostAsJsonAsync("/api/v1/auth/register", new RegisterRequest
        {
            Email = email,
            Password = password,
            DisplayName = "Test User",
        });
        response.EnsureSuccessStatusCode();
    }
}
