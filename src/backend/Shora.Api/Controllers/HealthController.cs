using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Shora.Contracts.Common;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public sealed class HealthController : ApiControllerBase
{
    private readonly HealthCheckService _healthCheckService;

    public HealthController(HealthCheckService healthCheckService)
    {
        _healthCheckService = healthCheckService;
    }

    [HttpGet]
    [EndpointName("Health.Get")]
    [EndpointSummary("Check API health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var report = await _healthCheckService.CheckHealthAsync(cancellationToken: cancellationToken);
        var status = report.Status == HealthStatus.Healthy ? "healthy" : "unhealthy";
        var response = new HealthResponse(status, DateTime.UtcNow);

        return report.Status == HealthStatus.Healthy
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }
}
