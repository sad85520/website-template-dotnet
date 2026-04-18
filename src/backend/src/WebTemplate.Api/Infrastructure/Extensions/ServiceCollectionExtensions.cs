using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using Microsoft.AspNetCore.OpenApi;
using WebTemplate.Api.Infrastructure.Data;
using WebTemplate.Api.Modules.Accounts.Models.Settings;
using WebTemplate.Api.Modules.Accounts.Repositories;
using WebTemplate.Api.Modules.Accounts.Repositories.Interfaces;
using WebTemplate.Api.Modules.Accounts.Services;
using WebTemplate.Api.Modules.Accounts.Services.Interfaces;

namespace WebTemplate.Api.Infrastructure.Extensions;

/// <summary>擴充 <see cref="IServiceCollection"/> 的靜態輔助類別，提供模組化的服務注冊方法。</summary>
public static class ServiceCollectionExtensions
{
    /// <summary>注冊 Entity Framework Core 資料庫環境，使用 SQL Server 連線字串。</summary>
    /// <param name="services">應用程式的服務集合。</param>
    /// <param name="config">應用程式設定，必須包含 <c>ConnectionStrings:DefaultConnection</c>。</param>
    /// <returns>同一個 <see cref="IServiceCollection"/> 以支援鏈式呼叫。</returns>
    /// <exception cref="InvalidOperationException">遺漏或為空的 <c>DefaultConnection</c> 連線字串會在啟動階段拋出。</exception>
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        // 與 AddJwtAuthentication 同樣的 fail-fast 策略：在啟動時驗證連線字串必填，
        // 避免 production 誤用空字串時，EF Core 在第一個請求才拋出晦澀的連線錯誤
        // （SqlException: "A network-related or instance-specific error..."），
        // 或在 dev 環境誤連到 localhost 預設 instance 而看似能跑但資料寫錯地方。
        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException(
                "Database connection string is not configured. Set 'ConnectionStrings:DefaultConnection' in configuration.");

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services;
    }

    /// <summary>注冊 JWT Bearer 驗證，並在啟動時驗證必要設定。</summary>
    /// <param name="services">應用程式的服務集合。</param>
    /// <param name="config">應用程式設定，必須包含 <c>Jwt</c> 區段。</param>
    /// <returns>同一個 <see cref="IServiceCollection"/> 以支援鏈式呼叫。</returns>
    /// <exception cref="InvalidOperationException">缺少 <c>Jwt</c> 設定區段或 Secret 為空時拋出。</exception>
    public static IServiceCollection AddJwtAuthentication(this IServiceCollection services, IConfiguration config)
    {
        // 在啟動階段立即驗證 JWT 設定，確保遺漏 Secret 時能快速失敗，
        // 而非在第一個請求到達時才拋出隱晦的簽章驗證錯誤。
        var jwtSettings = config.GetSection("Jwt").Get<JwtSettings>()
            ?? throw new InvalidOperationException("JWT configuration section 'Jwt' is missing.");

        if (string.IsNullOrWhiteSpace(jwtSettings.Secret))
            throw new InvalidOperationException("JWT secret key is not configured. Set 'Jwt:Secret' in configuration.");

        services.Configure<JwtSettings>(config.GetSection("Jwt"));

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings.Issuer,
                    ValidAudience = jwtSettings.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtSettings.Secret)),
                    // 預設 ClockSkew 為 5 分鐘，會讓已過期的 token 仍可使用，
                    // 設為 Zero 以嚴格遵守 AccessTokenExpirationMinutes 設定。
                    ClockSkew = TimeSpan.Zero,
                };
            });

        return services;
    }

    /// <summary>注冊應用程式層的 Repository 與 Service，生命週期為 Scoped。</summary>
    /// <param name="services">應用程式的服務集合。</param>
    /// <returns>同一個 <see cref="IServiceCollection"/> 以支援鏈式呼叫。</returns>
    public static IServiceCollection AddApplicationServices(this IServiceCollection services)
    {
        // Repositories
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();

        // Services
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();

        return services;
    }

    /// <summary>注冊 OpenAPI 文件產生器，並設定 Bearer Token 安全方案與操作安全需求。</summary>
    /// <param name="services">應用程式的服務集合。</param>
    /// <returns>同一個 <see cref="IServiceCollection"/> 以支援鏈式呼叫。</returns>
    public static IServiceCollection AddOpenApiWithJwt(this IServiceCollection services)
    {
        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, context, cancellationToken) =>
            {
                document.Info = new() { Title = "WebTemplate API", Version = "v1" };
                document.Components ??= new OpenApiComponents();
                document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
                document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
                {
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Enter JWT token",
                };
                return Task.CompletedTask;
            });

            // 為所有操作加入 Bearer 安全需求，讓 Scalar UI 預設顯示鎖頭圖示。
            // 這僅影響文件顯示，不影響實際的授權驗證邏輯。
            options.AddOperationTransformer((operation, context, cancellationToken) =>
            {
                operation.Security ??= [];
                operation.Security.Add(new OpenApiSecurityRequirement
                {
                    [new OpenApiSecuritySchemeReference("Bearer")] = []
                });
                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>注冊速率限制器，包含 <c>auth</c>（嚴格，防暴力破解）與 <c>api</c>（寬鬆）兩個策略。</summary>
    /// <param name="services">應用程式的服務集合。</param>
    /// <returns>同一個 <see cref="IServiceCollection"/> 以支援鏈式呼叫。</returns>
    public static IServiceCollection AddRateLimiting(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            // "auth" 策略：限制登入/註冊端點，防止暴力破解。
            // QueueLimit = 0 表示超出限制的請求直接拒絕，不排隊等候。
            options.AddFixedWindowLimiter("auth", limiter =>
            {
                limiter.PermitLimit = 10;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            // "api" 策略：一般 API 的寬鬆限制，與 "auth" 分開設定，
            // 讓認證端點可以有更嚴格的獨立配額。
            options.AddFixedWindowLimiter("api", limiter =>
            {
                limiter.PermitLimit = 60;
                limiter.Window = TimeSpan.FromMinutes(1);
                limiter.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
                limiter.QueueLimit = 0;
            });

            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        });

        return services;
    }
}
