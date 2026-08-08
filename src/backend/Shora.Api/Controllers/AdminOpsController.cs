using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Common;
using Shora.Application.Services;
using Shora.Contracts.Ops;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/ops")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminOpsController(AdminOpsMonitoringService adminOpsMonitoringService) : ApiControllerBase
{
    [HttpGet("alerts")]
    [EndpointName("AdminOps.GetAlerts")]
    [EndpointSummary("Get active operational alerts for the admin dashboard")]
    [ProducesResponseType(typeof(AdminOpsAlertsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetAlerts(CancellationToken cancellationToken)
    {
        var response = await adminOpsMonitoringService.GetAlertsAsync(cancellationToken);
        return Ok(response);
    }
}
