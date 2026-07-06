using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Respawn;
using Testcontainers.MsSql;
using WebTemplate.Api.Infrastructure.Data;

namespace WebTemplate.Api.Tests.Integration;

/// <summary>
/// 整合測試用的 WebApplicationFactory。
/// 重點設計：
/// <list type="bullet">
///   <item>環境設為 <c>Testing</c>，跳過 Program.cs 中的 auto-migrate（改由本類別自行執行一次
///         <c>MigrateAsync</c>，確保 EF Core Migration 腳本本身在真實 SQL Server 上也驗證得到）。</item>
///   <item>透過 in-memory configuration 注入測試用 JWT 設定，避免真正的 secret 落入 source control。</item>
///   <item>資料庫改用 <b>Testcontainers.MsSql</b> 啟動的真實 SQL Server 容器，而非 SQLite in-memory
///         ——decimal 精度、collation、rowversion 等行為與 production 的 SQL Server 完全一致，
///         見 <c>docs/adr/ADR-005-testcontainers-for-integration-tests.md</c>。</item>
///   <item>本類別是 <see cref="IntegrationTestCollection"/> 的 collection fixture，
///         每次測試執行只會啟動<b>一個</b>容器並由整個 test collection 共用；
///         測試之間改用 <see cref="ResetDatabaseAsync"/>（Respawn）清空資料表，
///         而非每個測試各自起一個容器（容器啟動成本遠高於清資料）。</item>
/// </list>
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly MsSqlContainer _container = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private Respawner _respawner = null!;

    /// <summary>啟動 SQL Server 容器、套用 EF Core Migration，並建立 Respawn 清資料工具。</summary>
    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        // 觸發 Services 存取會依序執行 ConfigureWebHost → Program.cs top-level statements，
        // 此時 _container 已啟動、GetConnectionString() 可回傳實際映射的 port。
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        // 刻意呼叫 MigrateAsync 而非 EnsureCreated：EnsureCreated 直接依 model 產生 schema、
        // 完全繞過 Migrations/ 資料夾，測試永遠不會發現「migration 檔本身壞掉」這種問題。
        await db.Database.MigrateAsync();

        await using var respawnConnection = new SqlConnection(_container.GetConnectionString());
        await respawnConnection.OpenAsync();
        _respawner = await Respawner.CreateAsync(respawnConnection, new RespawnerOptions
        {
            DbAdapter = DbAdapter.SqlServer,
            SchemasToInclude = ["dbo"],
            TablesToIgnore = ["__EFMigrationsHistory"],
        });
    }

    /// <summary>清空除 Migration 歷史紀錄表以外的所有資料表，讓下一個測試從乾淨狀態開始。</summary>
    public async Task ResetDatabaseAsync()
    {
        await using var connection = new SqlConnection(_container.GetConnectionString());
        await connection.OpenAsync();
        await _respawner.ResetAsync(connection);
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
            // 必須完整移除 Program.cs 註冊的 SQL Server provider 相關服務並重新指向測試容器，
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
                options.UseSqlServer(_container.GetConnectionString()));

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

    /// <inheritdoc/>
    async Task IAsyncLifetime.DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

/// <summary>
/// 整合測試的 xUnit collection 定義。所有整合測試類別都應標註
/// <c>[Collection(IntegrationTestCollection.Name)]</c>，讓它們共用同一個
/// <see cref="CustomWebApplicationFactory"/> 實例（同一個 SQL Server 容器），
/// 而不是每個測試類別各自啟動一個容器。
/// </summary>
[CollectionDefinition(Name)]
public sealed class IntegrationTestCollection : ICollectionFixture<CustomWebApplicationFactory>
{
    public const string Name = "Integration";
}
