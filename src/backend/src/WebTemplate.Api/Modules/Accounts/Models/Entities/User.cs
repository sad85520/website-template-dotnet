namespace WebTemplate.Api.Modules.Accounts.Models.Entities;

/// <summary>系統使用者實體，對應資料庫 accounts 資料表。</summary>
public class User
{
    /// <summary>使用者唯一識別碼（UUID），預設於建立時自動產生。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>使用者電子郵件，同時作為登入帳號，全系統唯一。</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Bcrypt 雜湊後的密碼，不存明文。</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>使用者顯示名稱。</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>使用者角色，決定系統層級存取權限。</summary>
    public UserRole Role { get; set; } = UserRole.User;

    /// <summary>帳號建立時間（UTC）。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>最後更新時間（UTC）；尚未更新時為 <c>null</c>。</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>連續登入失敗次數，達閾值時觸發帳號鎖定。</summary>
    public int FailedLoginAttempts { get; set; } = 0;

    /// <summary>帳號鎖定截止時間（UTC）；未鎖定時為 <c>null</c>。</summary>
    public DateTime? LockoutUntil { get; set; }

    /// <summary>使用者持有的 Refresh Token 集合（EF Core 導覽屬性）。</summary>
    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
}

/// <summary>使用者角色列舉，控制 API 存取層級。</summary>
public enum UserRole
{
    /// <summary>一般使用者，僅可存取自身資料。</summary>
    User,

    /// <summary>系統管理員，可存取所有使用者資料與管理功能。</summary>
    Admin
}
