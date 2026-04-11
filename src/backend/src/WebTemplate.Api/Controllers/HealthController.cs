using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace WebTemplate.Api.Controllers;

[ApiController]
[Route("api/health")]
public class HealthController(HealthCheckService healthCheckService) : ControllerBase
{
    // Liveness：只確認程序本身是否存活，不檢查任何依賴項，
    // Kubernetes 以此決定是否重啟 pod（不應因 DB 暫時斷線而重啟）。
    [HttpGet]
    public IActionResult Liveness() => Ok(new { status = "healthy" });

    // Readiness：確認應用程式的所有依賴（目前為資料庫）是否就緒，
    // Kubernetes 以此決定是否將流量導入此 pod（未就緒時不接收請求）。
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
