using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Shora.Application.AdminSettings;
using Shora.Application.Common;
using Shora.Application.Services;
using Shora.Contracts.Settings;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/admin/settings")]
[Authorize(Roles = AppRoles.Admin)]
public sealed class AdminSettingsController(SettingsService settingsService) : ApiControllerBase
{
    [HttpGet]
    [EndpointName("AdminSettings.Get")]
    [EndpointSummary("Get the full consultant settings singleton")]
    [ProducesResponseType(typeof(AdminSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(CancellationToken cancellationToken)
    {
        var result = await settingsService.GetAdminAsync(cancellationToken);
        return FromResult(result);
    }

    [HttpPut]
    [EndpointName("AdminSettings.Update")]
    [EndpointSummary("Update the consultant settings singleton")]
    [ProducesResponseType(typeof(AdminSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(
        [FromBody] UpdateAdminSettingsRequest request,
        CancellationToken cancellationToken)
    {
        var validation = SettingsUpdateValidator.Validate(request);
        if (!validation.IsValid)
        {
            return FromValidationErrors(validation.Errors);
        }

        var result = await settingsService.UpdateAsync(validation.Value!, cancellationToken);
        return FromResult(result);
    }
}
