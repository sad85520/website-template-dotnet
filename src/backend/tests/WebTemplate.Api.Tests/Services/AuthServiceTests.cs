using Moq;
using WebTemplate.Api.Common.Exceptions;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Tests.Services;

/// <summary>
/// <see cref="AuthService"/> 的單元測試，驗證使用者註冊、登入、帳號鎖定與失敗計數重置邏輯。
/// <see cref="IUserRepository"/> 與 <see cref="ITokenService"/> 皆以 Moq 替代，聚焦在
/// <see cref="AuthService"/> 本身的業務邏輯，不需要任何資料庫（真實或 in-memory）。
/// 需要真實 SQL Server 行為（約束、交易、遷移）的驗證交給整合測試
/// （見 <c>Integration/AuthControllerIntegrationTests.cs</c>，以 Testcontainers 啟動）。
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<IUserRepository> _userRepoMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly AuthService _service;

    public AuthServiceTests()
    {
        _service = new AuthService(_userRepoMock.Object, _tokenServiceMock.Object);
    }

    /// <summary>有效資料呼叫 RegisterAsync 應建立使用者並回傳包含正確 Email、DisplayName 與 role="user" 的 UserDto。</summary>
    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsUserDto()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _userRepoMock.Setup(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((User u, CancellationToken _) => u);

        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            DisplayName = "Test User",
        };

        var result = await _service.RegisterAsync(request);

        Assert.Equal(request.Email, result.Email);
        Assert.Equal(request.DisplayName, result.DisplayName);
        Assert.Equal("user", result.Role);
    }

    /// <summary>Email 已存在時呼叫 RegisterAsync 應拋出 AppConflictException。</summary>
    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsAppConflictException()
    {
        _userRepoMock.Setup(r => r.ExistsByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var request = new RegisterRequest
        {
            Email = "dup@example.com",
            Password = "Password123!",
            DisplayName = "User",
        };

        await Assert.ThrowsAsync<AppConflictException>(() => _service.RegisterAsync(request));
        _userRepoMock.Verify(r => r.CreateAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>以錯誤密碼呼叫 LoginAsync 應拋出 AppAuthenticationException。</summary>
    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsAppAuthenticationException()
    {
        var user = new User
        {
            Email = "login@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword!"),
            DisplayName = "User",
        };
        _userRepoMock.Setup(r => r.FindByEmailAsync("login@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        await Assert.ThrowsAsync<AppAuthenticationException>(() =>
            _service.LoginAsync(new LoginRequest
            {
                Email = "login@example.com",
                Password = "WrongPassword!",
            }));
    }

    /// <summary>正確憑證呼叫 LoginAsync 應回傳使用者資料與 access/refresh token 三元組。</summary>
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        var user = new User
        {
            Email = "user@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "User",
        };
        _userRepoMock.Setup(r => r.FindByEmailAsync("user@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(t => t.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenPair(
                new RefreshToken { TokenHash = "refresh-hash", ExpiresAt = DateTime.UtcNow.AddDays(7) },
                "refresh-token"));

        var (resultUser, accessToken, refreshToken) = await _service.LoginAsync(new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!",
        });

        Assert.Equal("user@example.com", resultUser.Email);
        Assert.Equal("access-token", accessToken);
        Assert.Equal("refresh-token", refreshToken);
    }

    /// <summary>連續五次密碼錯誤後，第六次呼叫 LoginAsync 應拋出含 "locked" 字樣的例外（帳號鎖定）。</summary>
    [Fact]
    public async Task LoginAsync_AfterFiveFailedAttempts_LocksAccount()
    {
        // FindByEmailAsync 每次都回傳「同一個」user 物件參照，讓 AuthService 對
        // FailedLoginAttempts / LockoutUntil 的原地修改能跨呼叫累積，
        // 模擬真實資料庫「讀出 → 修改 → UpdateAsync 寫回」的持久化效果，而不需要真的接資料庫。
        var user = new User
        {
            Email = "lockout@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword!"),
            DisplayName = "User",
        };
        _userRepoMock.Setup(r => r.FindByEmailAsync("lockout@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<AppAuthenticationException>(() =>
                _service.LoginAsync(new LoginRequest
                {
                    Email = "lockout@example.com",
                    Password = "WrongPassword!",
                }));
        }

        var ex = await Assert.ThrowsAsync<AppAuthenticationException>(() =>
            _service.LoginAsync(new LoginRequest
            {
                Email = "lockout@example.com",
                Password = "WrongPassword!",
            }));

        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>LockoutUntil 未過期的帳號即使密碼正確也應拋出含 "locked" 字樣的 AppAuthenticationException。</summary>
    [Fact]
    public async Task LoginAsync_WithLockedAccount_ThrowsAppAuthenticationException()
    {
        var user = new User
        {
            Email = "locked@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Locked User",
            LockoutUntil = DateTime.UtcNow.AddMinutes(15),
        };
        _userRepoMock.Setup(r => r.FindByEmailAsync("locked@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<AppAuthenticationException>(() =>
            _service.LoginAsync(new LoginRequest
            {
                Email = "locked@example.com",
                Password = "Password123!",
            }));

        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>登入成功後應將 FailedLoginAttempts 重置為 0。</summary>
    [Fact]
    public async Task LoginAsync_SuccessfulLogin_ResetsFailedAttempts()
    {
        var user = new User
        {
            Email = "resetattempts@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "User",
            FailedLoginAttempts = 3,
        };
        _userRepoMock.Setup(r => r.FindByEmailAsync("resetattempts@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");
        _tokenServiceMock
            .Setup(t => t.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshTokenPair(
                new RefreshToken { TokenHash = "refresh-hash", ExpiresAt = DateTime.UtcNow.AddDays(7) },
                "refresh-token"));

        await _service.LoginAsync(new LoginRequest
        {
            Email = "resetattempts@example.com",
            Password = "Password123!",
        });

        // user 是 FindByEmailAsync 回傳的同一個參照，AuthService 對其欄位的原地修改
        // 可以直接在這裡斷言，等同於原先「重新從 DB 查回來確認已寫入」的驗證意圖。
        Assert.Equal(0, user.FailedLoginAttempts);
    }
}
