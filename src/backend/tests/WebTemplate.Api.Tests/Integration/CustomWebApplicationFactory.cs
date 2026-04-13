using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using WebTemplate.Api.Infrastructure.Data;

namespace WebTemplate.Api.Tests.Integration;

/// <summary>
/// 整合測試用的 WebApplicationFactory。
/// 重點調整：
/// <list type="bullet">
///   <item>環境設為 <c>Testing</c>，跳過 Program.cs 中的 auto-migrate（InMemory 不支援 MigrateAsync）。</item>
///   <item>透過 in-memory configuration 注入測試用 JWT 設定，避免真正的 secret 落入 source control。</item>
///   <item>移除既有的 SQL Server <see cref="AppDbContext"/> 註冊，改用 InMemory provider，
///         確保每個 test class 都有獨立的資料庫實例。</item>
/// </list>
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"IntegrationTestDb_{Guid.NewGuid()}";

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
                options.UseInMemoryDatabase(_dbName));

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
}
