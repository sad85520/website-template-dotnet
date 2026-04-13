using Moq;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories;
using WebTemplate.Api.Modules.Accounts.Services;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;
using WebTemplate.Api.Tests.Helpers;

namespace WebTemplate.Api.Tests.Services;

/// <summary>
/// <see cref="AuthService"/> 的單元測試，驗證使用者註冊、登入、帳號鎖定與失敗計數重置邏輯。
/// 使用 InMemory EF Core 資料庫；Token 相關依賴以 Moq 替代以聚焦在認證業務邏輯本身。
/// </summary>
public class AuthServiceTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();

    /// <summary>有效資料呼叫 RegisterAsync 應建立使用者並回傳包含正確 Email、DisplayName 與 role="user" 的 UserDto。</summary>
    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsUserDto()
    {
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        var request = new RegisterRequest
        {
            Email = "test@example.com",
            Password = "Password123!",
            DisplayName = "Test User",
        };

        var result = await service.RegisterAsync(request);

        Assert.Equal(request.Email, result.Email);
        Assert.Equal(request.DisplayName, result.DisplayName);
        Assert.Equal("user", result.Role);
    }

    /// <summary>以相同 Email 重複呼叫 RegisterAsync 應拋出 InvalidOperationException。</summary>
    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ThrowsInvalidOperationException()
    {
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        var request = new RegisterRequest
        {
            Email = "dup@example.com",
            Password = "Password123!",
            DisplayName = "User",
        };

        await service.RegisterAsync(request);

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RegisterAsync(request));
    }

    /// <summary>以錯誤密碼呼叫 LoginAsync 應拋出 UnauthorizedAccessException。</summary>
    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorizedAccessException()
    {
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        await service.RegisterAsync(new RegisterRequest
        {
            Email = "login@example.com",
            Password = "CorrectPassword!",
            DisplayName = "User",
        });

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest
            {
                Email = "login@example.com",
                Password = "WrongPassword!",
            }));
    }

    /// <summary>正確憑證呼叫 LoginAsync 應回傳使用者資料與 access/refresh token 三元組。</summary>
    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokens()
    {
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(t => t.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { TokenHash = "refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(7) });

        await service.RegisterAsync(new RegisterRequest
        {
            Email = "user@example.com",
            Password = "Password123!",
            DisplayName = "User",
        });

        var (user, accessToken, refreshToken) = await service.LoginAsync(new LoginRequest
        {
            Email = "user@example.com",
            Password = "Password123!",
        });

        Assert.Equal("user@example.com", user.Email);
        Assert.Equal("access-token", accessToken);
        Assert.Equal("refresh-token", refreshToken);
    }

    /// <summary>連續五次密碼錯誤後，第六次呼叫 LoginAsync 應拋出含 "locked" 字樣的例外（帳號鎖定）。</summary>
    [Fact]
    public async Task LoginAsync_AfterFiveFailedAttempts_LocksAccount()
    {
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        await service.RegisterAsync(new RegisterRequest
        {
            Email = "lockout@example.com",
            Password = "CorrectPassword!",
            DisplayName = "User",
        });

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                service.LoginAsync(new LoginRequest
                {
                    Email = "lockout@example.com",
                    Password = "WrongPassword!",
                }));
        }

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest
            {
                Email = "lockout@example.com",
                Password = "WrongPassword!",
            }));

        Assert.Contains("locked", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>LockoutUntil 未過期的帳號即使密碼正確也應拋出含 "locked" 字樣的 UnauthorizedAccessException。</summary>
    [Fact]
    public async Task LoginAsync_WithLockedAccount_ThrowsUnauthorizedAccessException()
    {
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        var user = new User
        {
            Email = "locked@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "Locked User",
            LockoutUntil = DateTime.UtcNow.AddMinutes(15),
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        var ex = await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            service.LoginAsync(new LoginRequest
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
        using var db = TestDbContextFactory.Create();
        var userRepo = new UserRepository(db);
        var service = new AuthService(userRepo, _tokenServiceMock.Object);

        _tokenServiceMock
            .Setup(t => t.GenerateAccessToken(It.IsAny<User>()))
            .Returns("access-token");

        _tokenServiceMock
            .Setup(t => t.CreateRefreshTokenAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new RefreshToken { TokenHash = "refresh-token", ExpiresAt = DateTime.UtcNow.AddDays(7) });

        var user = new User
        {
            Email = "resetattempts@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Password123!"),
            DisplayName = "User",
            FailedLoginAttempts = 3,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();

        await service.LoginAsync(new LoginRequest
        {
            Email = "resetattempts@example.com",
            Password = "Password123!",
        });

        var savedUser = await db.Users.FindAsync(user.Id);
        Assert.Equal(0, savedUser!.FailedLoginAttempts);
    }
}
