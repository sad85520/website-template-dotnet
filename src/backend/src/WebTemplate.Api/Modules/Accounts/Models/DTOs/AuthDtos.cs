using System.ComponentModel.DataAnnotations;

namespace WebTemplate.Api.Modules.Accounts.Models.DTOs;

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // MinLength(8) 是最低防線，生產環境應搭配更嚴格的密碼複雜度規則。
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    [Required, MinLength(2), MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;
}

// ExpiresIn 單位為秒，與 OAuth 2.0 標準一致，
// 客戶端依此值設定自動刷新 access token 的計時器。
public sealed record LoginResponse(string AccessToken, int ExpiresIn);

// UserDto 不含 PasswordHash 等敏感欄位；
// Role 以小寫字串回傳（見 AuthService.MapToDto），前端可直接做字串比對。
public sealed record UserDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Role { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
}
