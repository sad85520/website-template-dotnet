using Microsoft.EntityFrameworkCore;
using WebTemplate.Api.Infrastructure.Data;

namespace WebTemplate.Api.Tests.Helpers;

/// <summary>提供 InMemory 資料庫的 <see cref="AppDbContext"/> 測試工廠，用於單元測試的資料庫隔離。</summary>
public static class TestDbContextFactory
{
    /// <summary>建立一個使用獨立 InMemory 資料庫的 <see cref="AppDbContext"/> 實例。</summary>
    /// <param name="dbName">資料庫名稱；省略時自動產生 GUID，確保每次呼叫都是全新的空資料庫。</param>
    /// <returns>已設定好的 <see cref="AppDbContext"/> 實例。</returns>
    public static AppDbContext Create(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}
