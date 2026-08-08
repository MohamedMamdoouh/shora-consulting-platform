using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.AspNetCore.RateLimiting;
using Shora.Api.Infrastructure;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Services;
using Shora.Contracts.Availability;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/availability")]
public sealed class AvailabilityController(AvailabilityService availabilityService) : ApiControllerBase
{
    [OutputCache(PolicyName = CachePolicies.PublicAvailability)]
    [HttpGet]
    [EnableRateLimiting(RateLimitPolicies.PublicAvailability)]
    [EndpointName("Availability.GetOpenSlots")]
    [EndpointSummary("Get open availability slots in a UTC date range")]
    [ProducesResponseType(typeof(AvailabilityResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> GetOpenSlots(
        [FromQuery] DateTime? from,
        [FromQuery] DateTime? to,
        CancellationToken cancellationToken)
    {
        if (!from.HasValue || !to.HasValue)
        {
            return ToProblem(Error.Validation(
                ErrorCodes.General.Validation,
                "Both 'from' and 'to' query parameters are required."));
        }

        var result = await availabilityService.GetOpenSlotsAsync(from.Value, to.Value, cancellationToken);
        return FromResult(result);
    }
}
