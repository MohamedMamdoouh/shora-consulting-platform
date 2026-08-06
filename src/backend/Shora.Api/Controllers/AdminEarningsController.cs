using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Common;
using Shora.Application.Earnings;
using Shora.Application.Services;
using Shora.Contracts.Payments;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/earnings")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminEarningsController(AdminEarningsService adminEarningsService) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("AdminEarnings.Get")]
    [EndpointSummary("Get earnings aggregates for the admin dashboard")]
    [ProducesResponseType(typeof(AdminEarningsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Get(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken = default)
    {
        var validation = AdminEarningsQueryValidator.Validate(new AdminEarningsQuery(from, to));

        if (!validation.IsValid)
        {
            return FromValidationErrors(validation.Errors);
        }

        var result = await adminEarningsService.GetAsync(validation.Value!, cancellationToken);
        return FromResult(result);
    }
}
