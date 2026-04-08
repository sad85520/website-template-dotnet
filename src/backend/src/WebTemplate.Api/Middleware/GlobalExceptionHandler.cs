using Microsoft.AspNetCore.Diagnostics;
using WebTemplate.Api.Models.DTOs.Common;

namespace WebTemplate.Api.Middleware;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Unhandled exception for {Method} {Path}",
            httpContext.Request.Method, httpContext.Request.Path);

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

        return true;
    }
}
