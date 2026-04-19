using WebTemplate.Api.Modules.Accounts.Models.Entities;

namespace WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;

/// <summary>使用者資料存取介面。</summary>
public interface IUserRepository
{
    /// <summary>依 ID 查詢使用者。</summary>
    /// <param name="id">使用者的 GUID。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>找到則回傳 <see cref="User"/>；否則回傳 <c>null</c>。</returns>
    Task<User?> FindByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>依電子郵件查詢使用者。</summary>
    /// <param name="email">使用者電子郵件（大小寫不敏感）。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>找到則回傳 <see cref="User"/>；否則回傳 <c>null</c>。</returns>
    Task<User?> FindByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>檢查指定電子郵件是否已被註冊。</summary>
    /// <param name="email">要查詢的電子郵件。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已存在則為 <c>true</c>；否則為 <c>false</c>。</returns>
    Task<bool> ExistsByEmailAsync(string email, CancellationToken ct = default);

    /// <summary>分頁查詢使用者列表，可選關鍵字搜尋。</summary>
    /// <param name="page">頁碼（從 1 開始）。</param>
    /// <param name="limit">每頁筆數。</param>
    /// <param name="search">可選的搜尋關鍵字，比對 Email 與 DisplayName。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>符合條件的總筆數與當前頁的使用者清單。</returns>
    Task<(int Total, IReadOnlyList<User> Items)> FindPagedAsync(
        int page, int limit, string? search, CancellationToken ct = default);

    /// <summary>將新使用者寫入資料庫。</summary>
    /// <param name="user">要建立的使用者實體。</param>
    /// <param name="ct">取消令牌。</param>
    /// <returns>已儲存的 <see cref="User"/> 實體。</returns>
    Task<User> CreateAsync(User user, CancellationToken ct = default);

    /// <summary>
    /// 將已追蹤的使用者實體變更（例如 <c>FailedLoginAttempts</c>、<c>LockoutUntil</c>）持久化至資料庫。
    /// 讓服務層以語意明確的方法名稱更新使用者狀態，避免直接暴露 EF Core 的 Unit-of-Work（<c>SaveChangesAsync</c>）。
    /// </summary>
    /// <param name="user">要更新的使用者實體（需先由查詢方法取得以便 EF 追蹤）。</param>
    /// <param name="ct">取消令牌。</param>
    Task UpdateAsync(User user, CancellationToken ct = default);
}
