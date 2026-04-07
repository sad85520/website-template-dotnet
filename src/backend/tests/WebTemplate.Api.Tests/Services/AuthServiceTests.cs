using Moq;
using WebTemplate.Api.Models.DTOs.Auth;
using WebTemplate.Api.Models.Entities;
using WebTemplate.Api.Services;
using WebTemplate.Api.Services.Interfaces;
using WebTemplate.Api.Tests.Helpers;

namespace WebTemplate.Api.Tests.Services;

public class AuthServiceTests
{
    private readonly Mock<ITokenService> _tokenServiceMock = new();

    [Fact]
    public async Task RegisterAsync_WithValidData_ReturnsUserDto()
    {
        using var db = TestDbContextFactory.Create();
        var service = new AuthService(db, _tokenServiceMock.Object);

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
        var service = new AuthService(db, _tokenServiceMock.Object);

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
        var service = new AuthService(db, _tokenServiceMock.Object);

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
        var service = new AuthService(db, _tokenServiceMock.Object);

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
}
