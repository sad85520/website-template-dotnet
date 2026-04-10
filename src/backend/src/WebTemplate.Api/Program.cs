using Microsoft.EntityFrameworkCore;
using NetEscapades.AspNetCore.SecurityHeaders;
using Scalar.AspNetCore;
using Serilog;
using WebTemplate.Api.Data;
using WebTemplate.Api.Extensions;
using WebTemplate.Api.Middleware;

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

    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();
    app.MapControllers();

    await app.RunAsync();
}
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
