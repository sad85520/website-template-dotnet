using Microsoft.EntityFrameworkCore;
using NetEscapades.AspNetCore.SecurityHeaders;
using Scalar.AspNetCore;
using Serilog;
using WebTemplate.Api.Infrastructure.Data;
using WebTemplate.Api.Infrastructure.Extensions;
using WebTemplate.Api.Infrastructure.Middleware;

// Bootstrap logger：在 DI 容器建立完成前提供最低限度的日誌能力，
// 確保 WebApplication.CreateBuilder 階段拋出的例外也能被記錄。
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Serilog
    builder.Host.UseSerilog((ctx, services, config) =>
        config.ReadFrom.Configuration(ctx.Configuration)
              .ReadFrom.Services(services)
              .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {CorrelationId} {Message:lj}{NewLine}{Exception}"));

    // Services
    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();

    // AddProblemDetails 必須與 AddExceptionHandler 搭配，
    // 才能在 handler 未攔截到例外時回退為標準 RFC 7807 Problem Details 格式。
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
    builder.Services.AddProblemDetails();

    builder.Services.AddDatabase(builder.Configuration);
    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddApplicationServices();
    builder.Services.AddOpenApiWithJwt();
    builder.Services.AddRateLimiting();

    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>("database");

    // CORS（僅開發環境）
    if (builder.Environment.IsDevelopment())
    {
        builder.Services.AddCors(options =>
        {
            options.AddDefaultPolicy(policy =>
                policy.WithOrigins("http://localhost:5173", "http://localhost")
                      .AllowAnyHeader()
                      .AllowAnyMethod()
                      .AllowCredentials());
        });
    }

    var app = builder.Build();

    // Auto-migrate（僅開發環境）
    // 生產環境應透過 CI/CD pipeline 或獨立的 migration job 執行，
    // 避免多個 pod 同時啟動時發生競態條件。
    if (app.Environment.IsDevelopment())
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    // Middleware pipeline
    // 嚴格 CSP：套用於所有非 Scalar/OpenAPI 路徑
    app.UseWhen(
        ctx => !IsDevUiPath(ctx),
        branch => branch.UseSecurityHeaders(policies => policies
            .AddDefaultSecurityHeaders()
            .AddContentSecurityPolicy(csp =>
            {
                csp.AddDefaultSrc().None();
                csp.AddObjectSrc().None();
                csp.AddScriptSrc().None();
                csp.AddStyleSrc().None();
                csp.AddImgSrc().Self().Data();
                csp.AddFontSrc().None();
                csp.AddConnectSrc().Self();
                csp.AddFrameAncestors().None();
            })));

    // Scalar 專用 CSP：僅開發環境，允許 jsdelivr CDN
    if (app.Environment.IsDevelopment())
    {
        app.UseWhen(
            IsDevUiPath,
            branch => branch.UseSecurityHeaders(policies => policies
                .AddDefaultSecurityHeaders()
                .AddContentSecurityPolicy(csp =>
                {
                    csp.AddDefaultSrc().None();
                    csp.AddScriptSrc().From("https://cdn.jsdelivr.net").UnsafeInline();
                    csp.AddStyleSrc().From("https://cdn.jsdelivr.net").UnsafeInline();
                    csp.AddImgSrc().Self().Data().From("https:");
                    csp.AddFontSrc().From("https://cdn.jsdelivr.net");
                    csp.AddConnectSrc().Self();
                    csp.AddFrameAncestors().None();
                })));
    }

    // UseExceptionHandler 必須排在所有業務 middleware 之前，
    // 否則後續 middleware 拋出的例外將無法被攔截。
    app.UseExceptionHandler();
    app.UseSerilogRequestLogging();

    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        app.MapScalarApiReference(options =>
        {
            options.Title = "WebTemplate API";
        });
        app.UseCors();
    }

    // middleware 順序有嚴格依賴：
    // RateLimiter → Authentication → Authorization
    // 若 Authentication 在 RateLimiter 之前，攻擊者可繞過速率限制先觸發 JWT 驗證邏輯。
    // Authorization 必須在 Authentication 之後，因為需要已解析的 ClaimsPrincipal。
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
// HostAbortedException 是 .NET 在 dotnet watch 重新啟動時主動拋出的，
// 不屬於異常終止，過濾掉可避免產生誤導性的 Fatal 日誌。
catch (Exception ex) when(ex is not HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}

static bool IsDevUiPath(HttpContext ctx) =>
    ctx.Request.Path.StartsWithSegments("/scalar") ||
    ctx.Request.Path.StartsWithSegments("/openapi");
