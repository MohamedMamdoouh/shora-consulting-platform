using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.Availability;
using Shora.Application.Common;
using Shora.Application.Services;
using Shora.Contracts.Availability;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/availability-windows")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminAvailabilityController(AdminAvailabilityService adminAvailabilityService) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("AdminAvailability.ListWindows")]
    [EndpointSummary("List recurring availability windows")]
    [ProducesResponseType(typeof(IReadOnlyList<AvailabilityWindowResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> List(CancellationToken cancellationToken)
    {
        var result = await adminAvailabilityService.ListWindowsAsync(cancellationToken);
        return FromResult(result);
    }

    [HttpPost]
    [EndpointName("AdminAvailability.CreateWindow")]
    [EndpointSummary("Create a recurring availability window and regenerate slots")]
    [ProducesResponseType(typeof(AvailabilityWindowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create(
        [FromBody] CreateAvailabilityWindowRequest request,
        CancellationToken cancellationToken)
    {
        var validation = AvailabilityWindowValidator.ValidateCreate(request);
        if (!validation.IsValid)
        {
            return FromValidationErrors(validation.Errors);
        }

        var result = await adminAvailabilityService.CreateWindowAsync(validation.Value!, cancellationToken);
        return FromResult(result);
    }

    [HttpPut("{id:guid}")]
    [EndpointName("AdminAvailability.UpdateWindow")]
    [EndpointSummary("Update a recurring availability window and regenerate slots")]
    [ProducesResponseType(typeof(AvailabilityWindowResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        Guid id,
        [FromBody] UpdateAvailabilityWindowRequest request,
        CancellationToken cancellationToken)
    {
        var validation = AvailabilityWindowValidator.ValidateUpdate(request);
        if (!validation.IsValid)
        {
            return FromValidationErrors(validation.Errors);
        }

        var result = await adminAvailabilityService.UpdateWindowAsync(id, validation.Value!, cancellationToken);
        return FromResult(result);
    }

    [HttpDelete("{id:guid}")]
    [EndpointName("AdminAvailability.DeleteWindow")]
    [EndpointSummary("Delete a recurring availability window and regenerate slots")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await adminAvailabilityService.DeleteWindowAsync(id, cancellationToken);
        return FromResult(result);
    }
}
