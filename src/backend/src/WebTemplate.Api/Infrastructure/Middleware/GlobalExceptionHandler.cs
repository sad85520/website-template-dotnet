using Microsoft.AspNetCore.Diagnostics;
using WebTemplate.Api.Common.Models;

namespace WebTemplate.Api.Infrastructure.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

        // 例外型別到 HTTP 狀態碼的對應是業務語意的約定：
        // - 服務層拋出 UnauthorizedAccessException 代表身分驗證/授權失敗
        // - InvalidOperationException 代表業務規則衝突（如信箱已存在）
        // - 500 的 message 刻意使用通用文字，避免將內部錯誤細節洩漏給客戶端；
        //   詳細資訊已透過上方 LogError 記錄於伺服器端日誌。
        var (statusCode, message) = exception switch
        {
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, exception.Message),
            InvalidOperationException   => (StatusCodes.Status409Conflict, exception.Message),
            KeyNotFoundException        => (StatusCodes.Status404NotFound, exception.Message),
            ArgumentException           => (StatusCodes.Status400BadRequest, exception.Message),
            _                           => (StatusCodes.Status500InternalServerError, "An unexpected error occurred.")
        };

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            ApiResponse<object>.Fail(message),
            cancellationToken);

        // 回傳 true 表示例外已被處理，框架不會再繼續傳播或寫入 ProblemDetails。
        return true;
    }
}
