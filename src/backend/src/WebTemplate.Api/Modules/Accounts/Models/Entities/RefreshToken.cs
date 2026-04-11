namespace WebTemplate.Api.Modules.Accounts.Models.Entities;

public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }
    public string? ReplacedByToken { get; set; }

    public bool IsExpired => DateTime.UtcNow >= ExpiresAt;
    public bool IsRevoked => RevokedAt != null;
    // IsActive 是唯讀計算屬性，不對應資料庫欄位；
    // 查詢時不能直接用 LINQ 篩選（會嘗試轉 SQL 而失敗），
    // 必須先從資料庫取出實體後再在記憶體中判斷。
    public bool IsActive => !IsRevoked && !IsExpired;

    // null! 告訴編譯器此導覽屬性由 EF Core 在查詢時填充，
    // 直接 new RefreshToken() 時不會初始化，呼叫前須確保已透過 Include 載入。
    public User User { get; set; } = null!;
}
