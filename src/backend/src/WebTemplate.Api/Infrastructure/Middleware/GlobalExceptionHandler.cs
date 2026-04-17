using Microsoft.AspNetCore.Diagnostics;
using WebTemplate.Api.Common.Exceptions;
using WebTemplate.Api.Common.Models;

namespace WebTemplate.Api.Infrastructure.Middleware;

/// <summary>
/// 全域例外處理器，將未捕獲的例外轉換為統一的 <see cref="ApiResponse{T}"/> 格式回應。
/// 採用白名單策略：僅 <see cref="AppException"/>（業務層刻意拋出、訊息已審核）會原樣揭露訊息給客戶端；
/// 其他任何框架/第三方例外都會被轉為通用的 500 錯誤，避免 stack trace、SQL、路徑等內部細節意外洩漏。
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    /// <inheritdoc/>
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        var (statusCode, message) = exception switch
        {
            // 白名單：僅業務層明確拋出、訊息經過審核的 AppException 可原樣對外揭露。
            AppException appEx => (appEx.StatusCode, appEx.Message),
            // 預設：一律回傳通用訊息，詳細資訊只留在伺服器端日誌。
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(message),
            cancellationToken);

        // 回傳 true 表示例外已被處理，框架不會再繼續傳播或寫入 ProblemDetails。
        return true;
    }
}
