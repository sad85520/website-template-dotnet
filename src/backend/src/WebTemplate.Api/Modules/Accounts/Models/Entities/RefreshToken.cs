namespace WebTemplate.Api.Modules.Accounts.Models.Entities;

/// <summary>Refresh Token 實體，儲存 SHA-256 雜湊後的 Token 值與有效期資訊。</summary>
public class RefreshToken
{
    /// <summary>Refresh Token 唯一識別碼。</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>持有此 Token 的使用者 ID。</summary>
    public Guid UserId { get; set; }

    /// <summary>Token 的 SHA-256 雜湊值；原始明文不儲存於資料庫。</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Token 到期時間（UTC）。</summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>Token 建立時間（UTC）。</summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Token 被撤銷的時間（UTC）；<c>null</c> 表示尚未撤銷。</summary>
    public DateTime? RevokedAt { get; set; }

    /// <summary>替換此 Token 的新 Token Hash；用於追蹤 Token 輪換鏈。</summary>
    public string? ReplacedByToken { get; set; }

    /// <summary>是否已過期。</summary>
    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;

    /// <summary>是否已被撤銷。</summary>
    public bool IsRevoked => RevokedAt != null;

    // IsActive 是唯讀計算屬性，不對應資料庫欄位；
    // 查詢時不能直接用 LINQ 篩選（會嘗試轉 SQL 而失敗），
    // 必須先從資料庫取出實體後再在記憶體中判斷。
    /// <summary>是否仍為有效 Token（未撤銷且未過期）。不對應資料庫欄位，不可直接用於 LINQ 查詢。</summary>
    public bool IsActive => !IsRevoked && !IsExpired;

    // null! 告訴編譯器此導覽屬性由 EF Core 在查詢時填充，
    // 直接 new RefreshToken() 時不會初始化，呼叫前須確保已透過 Include 載入。
    /// <summary>導覽屬性，關聯的使用者實體；須透過 <c>Include</c> 載入，否則為 <c>null</c>。</summary>
    public User User { get; set; } = null!;
}
