using Moq;
using WebTemplate.Api.Modules.Accounts.Models.DTOs;
using WebTemplate.Api.Modules.Accounts.Models.Entities;
using WebTemplate.Api.Modules.Accounts.Repositories;
using WebTemplate.Api.Modules.Accounts.Services;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;
using WebTemplate.Api.Tests.Helpers;

namespace WebTemplate.Api.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();

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
