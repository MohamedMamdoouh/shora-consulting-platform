using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;
using Shora.Application.Common;
using Shora.Application.Services;
using Shora.Contracts.Settings;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/settings")]
public sealed class SettingsController(SettingsService settingsService) : ApiControllerBase
{
    [OutputCache(PolicyName = CachePolicies.PublicSettings)]
    [HttpGet("public")]
    [EndpointName("Settings.GetPublic")]
    [EndpointSummary("Get public session price and duration")]
    [ProducesResponseType(typeof(PublicSettingsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPublic(CancellationToken cancellationToken)
    {
        var result = await settingsService.GetPublicAsync(cancellationToken);
        return FromResult(result);
    }
}
