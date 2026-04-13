using System.ComponentModel.DataAnnotations;

namespace WebTemplate.Api.Modules.Accounts.Models.DTOs;

/// <summary>登入請求資料傳輸物件。</summary>
public class LoginRequest
{
    /// <summary>使用者電子郵件，必須是有效的 email 格式。</summary>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    // MinLength(8) 是最低防線，生產環境應搭配更嚴格的密碼複雜度規則。
    /// <summary>使用者密碼，最少 8 個字元。</summary>
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;
}

/// <summary>使用者註冊請求資料傳輸物件。</summary>
public class RegisterRequest
{
    /// <summary>使用者電子郵件，必須是有效的 email 格式且尚未被註冊。</summary>
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    /// <summary>使用者密碼，最少 8 個字元。</summary>
    [Required, MinLength(8)]
    public string Password { get; set; } = string.Empty;

    /// <summary>顯示名稱，2 至 50 個字元。</summary>
    [Required, MinLength(2), MaxLength(50)]
    public string DisplayName { get; set; } = string.Empty;
}

// ExpiresIn 單位為秒，與 OAuth 2.0 標準一致，
// 客戶端依此值設定 token 自動更新的計時器。
/// <summary>登入成功的回應物件，包含 access token 及有效時間。</summary>
/// <param name="AccessToken">已簽署的 JWT access token 字串。</param>
/// <param name="ExpiresIn">Token 有效秒數（與 OAuth 2.0 標準一致）。</param>
public sealed record LoginResponse(string AccessToken, int ExpiresIn);

// UserDto 不含 PasswordHash 等敏感欄位；
// Role 以小寫字串回傳（見 AuthService.MapToDto），前端可直接做字串比對。
/// <summary>使用者資料傳輸物件，不含密碼等敏感欄位。</summary>
public sealed record UserDto
{
    /// <summary>使用者唯一識別碼。</summary>
    public Guid Id { get; init; }

    /// <summary>使用者電子郵件。</summary>
    public string Email { get; init; } = string.Empty;

    /// <summary>使用者顯示名稱。</summary>
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>使用者角色，小寫字串格式（例如 <c>user</c>、<c>admin</c>）。</summary>
    public string Role { get; init; } = string.Empty;

    /// <summary>帳號建立時間（UTC）。</summary>
    public DateTime CreatedAt { get; init; }
}
