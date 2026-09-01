using Microsoft.AspNetCore.Mvc;
using Shora.Contracts.Common;
using Shora.Infrastructure.Data;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public sealed class HealthController(ApplicationDbContext dbContext) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("Health.Get")]
    [EndpointSummary("Check API health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var timestamp = DateTime.UtcNow;
        if (!await dbContext.Database.CanConnectAsync(cancellationToken))
        {
            return StatusCode(
                StatusCodes.Status503ServiceUnavailable,
                new HealthResponse("unhealthy", timestamp));
        }

        return Ok(new HealthResponse("healthy", timestamp));
    }
}
