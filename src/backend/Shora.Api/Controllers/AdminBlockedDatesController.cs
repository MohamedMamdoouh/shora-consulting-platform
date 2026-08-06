using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Availability;
using Shora.Application.Common;
using Shora.Application.Services;
using Shora.Contracts.Availability;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/blocked-dates")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminBlockedDatesController(AdminBlockedDateService adminBlockedDateService) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("AdminBlockedDates.List")]
    [EndpointSummary("List blocked date ranges")]
    [ProducesResponseType(typeof(IReadOnlyList<BlockedDateResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await adminBlockedDateService.ListAsync(cancellationToken);
        return FromResult(result);
    }

    [HttpPost]
    [EndpointName("AdminBlockedDates.Create")]
    [EndpointSummary("Block a date range and remove overlapping open slots")]
    [ProducesResponseType(typeof(BlockedDateResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateBlockedDateRequest request,
        CancellationToken cancellationToken)
    {
        var validation = BlockedDateValidator.ValidateCreate(request);
        if (!validation.IsValid)
        {
            return FromValidationErrors(validation.Errors);
        }

        var result = await adminBlockedDateService.CreateAsync(validation.Value!, cancellationToken);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    [EndpointName("AdminBlockedDates.Delete")]
    [EndpointSummary("Remove a blocked date range and regenerate open slots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminBlockedDateService.DeleteAsync(id, cancellationToken);
        return FromResult(result);
    }
}
