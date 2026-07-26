using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Shora.Contracts.Common;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/[controller]")]
public sealed class HealthController : ApiControllerBase
{
    [HttpGet]
    [EndpointName("Health.Get")]
    [EndpointSummary("Check API health")]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public IActionResult Get()
    {
        return Ok(new HealthResponse("healthy", DateTime.UtcNow));
    }
}
