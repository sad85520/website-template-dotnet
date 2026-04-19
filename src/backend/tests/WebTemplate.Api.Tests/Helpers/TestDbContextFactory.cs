using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Infrastructure.Data;

namespace WebTemplate.Api.Tests.Helpers;

/// <summary>
/// 提供 SQLite in-memory 資料庫的 <see cref="AppDbContext"/> 測試工廠，用於單元測試的資料庫隔離。
/// </summary>
/// <remarks>
/// 之前使用 <c>UseInMemoryDatabase</c>，但它會跳過 SQL 約束驗證（unique / FK / check），
/// 讓測試通過但 production 在同樣資料下拋 <c>DbUpdateException</c>。改用 SQLite in-memory
/// 後，EF Core 會真正翻成 CREATE TABLE 與 UNIQUE INDEX DDL，確保測試抓得到約束違反。
/// </remarks>
public static class TestDbContextFactory
{
    /// <summary>
    /// 建立一個使用獨立 SQLite in-memory 資料庫的 <see cref="AppDbContext"/> 實例。
    /// </summary>
    /// <remarks>
    /// 回傳的 context 內部持有一條 <see cref="SqliteConnection"/>；
    /// ":memory:" 資料庫生命週期與連線綁定，因此呼叫端在釋放 context 時會連同資料庫一起銷毀。
    /// 每次呼叫都拿到全新空資料庫，達到單元測試的隔離性。
    /// </remarks>
    /// <returns>已套用 schema（<c>EnsureCreated</c>）的 <see cref="AppDbContext"/>。</returns>
    public static AppDbContext Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        var context = new AppDbContext(options);
        context.Database.EnsureCreated();
        return context;
    }
}
