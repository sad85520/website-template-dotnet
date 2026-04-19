using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using WebTemplate.Api.Infrastructure.Data;

namespace WebTemplate.Api.Tests.Integration;

/// <summary>
/// 整合測試用的 WebApplicationFactory。
/// 重點調整：
/// <list type="bullet">
///   <item>環境設為 <c>Testing</c>，跳過 Program.cs 中的 auto-migrate。</item>
///   <item>透過 in-memory configuration 注入測試用 JWT 設定，避免真正的 secret 落入 source control。</item>
///   <item>移除既有的 SQL Server <see cref="AppDbContext"/> 註冊，改用 <b>SQLite in-memory</b> provider：
///         與 EF Core InMemory 不同，SQLite in-memory 會真正執行 SQL、驗證 unique / FK 等約束，
///         能抓到 InMemory 放水的違反 constraint 情境（例如 email unique 失敗時拋 DbUpdateException），
///         對應到 <c>AuthService.IsUniqueViolation</c> 的 409 轉譯路徑是否真的生效。</item>
///   <item>每個 test class 各自持有一條 <see cref="SqliteConnection"/>；連線存活期 = 資料庫存活期，
///         確保併行測試互不污染，但同一個 test class 內多個請求能共用同一份資料。</item>
/// </list>
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    // SQLite ":memory:" 資料庫的生命週期與 connection 綁定：connection 關閉 = DB 消失。
    // 因此必須在 factory 層級保留一條長連線，讓 EF Core 從 DI 拿到 scoped connection 時
    // 仍能命中同一份 in-memory DB；否則每次 AddDbContext 都會開一條新連線，拿到的是空 DB。
    private readonly SqliteConnection _connection;

    public CustomWebApplicationFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    /// <inheritdoc/>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Testing 環境同時關閉 auto-migrate、Scalar UI 與 CORS，
        // 讓測試直接命中 production-like 的 middleware 組合。
        builder.UseEnvironment("Testing");

        // WebApplication.CreateBuilder 會在 top-level statements 中「立即」讀取 Configuration，
        // ConfigureAppConfiguration 於此模式下的 callback 套用時機太晚，
        // 會導致 AddJwtAuthentication 讀到空字串而拋出。
        // UseSetting 寫入 IWebHostBuilder 的 host configuration，於 CreateBuilder 階段即生效。
        builder.UseSetting("ConnectionStrings:DefaultConnection", "Server=(localdb)\\unused;Database=unused");
        builder.UseSetting("Jwt:Secret", "integration-test-secret-key-must-be-long-enough-for-hmac-sha256-signing");
        builder.UseSetting("Jwt:Issuer", "WebTemplate.Test");
        builder.UseSetting("Jwt:Audience", "WebTemplate.Test");
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "15");
        builder.UseSetting("Jwt:RefreshTokenExpirationDays", "7");

        builder.ConfigureTestServices(services =>
        {
            // 必須完整移除 Program.cs 註冊的 SQL Server provider 相關服務，
            // 否則 EF Core 10 會拋出「兩個 provider 同時註冊」的 InvalidOperationException。
            // 單純移除 DbContextOptions<AppDbContext> 不夠 —— provider extension 服務仍殘留。
            var efServicesToRemove = services
                .Where(s =>
                    s.ServiceType == typeof(AppDbContext) ||
                    s.ServiceType == typeof(DbContextOptions<AppDbContext>) ||
                    s.ServiceType == typeof(DbContextOptions) ||
                    (s.ServiceType.FullName?.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal) ?? false))
                .ToList();

            foreach (var descriptor in efServicesToRemove)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            // 放寬 rate limiter：正式設定為 auth 10 req/min、api 60 req/min，
            // 整合測試在極短時間內打多次請求會觸發限流，干擾驗證目標。
            // 因 RateLimiterOptions 的 Policies 為內部 dict，無法直接重寫；
            // 改為移除既有的 IConfigureOptions<RateLimiterOptions> 註冊，再以無上限的 policy 重新掛上。
            var rlConfigDescriptors = services
                .Where(s => s.ServiceType == typeof(IConfigureOptions<RateLimiterOptions>) ||
                            s.ServiceType == typeof(IPostConfigureOptions<RateLimiterOptions>))
                .ToList();
            foreach (var d in rlConfigDescriptors)
                services.Remove(d);

            services.AddRateLimiter(options =>
            {
                options.AddFixedWindowLimiter("auth", l =>
                {
                    l.PermitLimit = int.MaxValue;
                    l.Window = TimeSpan.FromMinutes(1);
                    l.QueueLimit = 0;
                });
                options.AddFixedWindowLimiter("api", l =>
                {
                    l.PermitLimit = int.MaxValue;
                    l.Window = TimeSpan.FromMinutes(1);
                    l.QueueLimit = 0;
                });
            });
        });
    }

    // 覆寫 CreateHost 讓 host 啟動後、第一個請求進來前建立 schema。
    // 不能在 ConfigureTestServices 內 BuildServiceProvider() 手動 EnsureCreated，
    // 那會在 Program.cs 的 top-level statements 完成前就分岔 DI 容器，
    // 導致 WebApplicationFactory 誤判「entry point 未建立 IHost」而中斷測試啟動。
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.EnsureCreated();
        return host;
    }

    // WebApplicationFactory 自己就實作 IDisposable；覆寫 Dispose 才有機會關掉
    // ":memory:" 連線，否則 xUnit 將 factory 作為 test class fixture 重複實例化時，
    // 每個 class 都會漏一條開著的 SQLite 連線（雖然測試結束後 process 退出會回收，
    // 但在同一 process 內跑多個整合測試 class 會累積）。
    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
