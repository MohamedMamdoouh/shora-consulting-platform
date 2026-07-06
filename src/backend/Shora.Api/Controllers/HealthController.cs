using Microsoft.AspNetCore.Mvc;

namespace Shora.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { status = "healthy", timestampUtc = DateTime.UtcNow });
    }
}
