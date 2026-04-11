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

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDatabase(this IServiceCollection services, IConfiguration config)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(config.GetConnectionString("DefaultConnection")));

        return services;
    }

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
