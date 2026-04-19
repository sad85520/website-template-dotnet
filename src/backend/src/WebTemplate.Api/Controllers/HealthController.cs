using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebTemplate.Api.Controllers;

/// <summary>應用程式健康檢查端點，供 Kubernetes Liveness 與 Readiness Probe 使用。</summary>
[ApiController]
[Route("api/health")]
// Health probes 由 Kubelet 匿名呼叫，必須顯式 AllowAnonymous 以繞過全域 FallbackPolicy；
// 否則會回 401 讓 pod 被誤判為不健康。
[AllowAnonymous]
public class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    // Liveness：只確認程序本身是否存活，不檢查任何依賴項，
    // Kubernetes 以此決定是否重啟 pod（不應因 DB 暫時斷線而重啟）。
    /// <summary>Liveness 探針：確認程序本身存活，不檢查外部依賴。</summary>
    /// <returns>HTTP 200 OK 含 <c>{ status: "healthy" }</c>。</returns>
    [HttpGet]
    public IActionResult Liveness() => Ok(new { status = "healthy" });

    // Readiness：確認應用程式的所有依賴（目前為資料庫）是否就緒，
    // Kubernetes 以此決定是否將流量導入此 pod（未就緒時不接收請求）。
    /// <summary>Readiness 探針：確認應用程式所有依賴（如資料庫）均已就緒。</summary>
    /// <param name="ct">取消權杖。</param>
    /// <returns>依賴就緒時 HTTP 200 OK；任一依賴失敗時 HTTP 503 Service Unavailable。</returns>
    [HttpGet("ready")]
    public async Task<IActionResult> Readiness(CancellationToken ct)
    {
        var report = await healthCheckService.CheckHealthAsync(ct);
        var response = new
        {
            status = report.Status.ToString().ToLowerInvariant(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString().ToLowerInvariant(),
                description = e.Value.Description,
            }),
        };

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
