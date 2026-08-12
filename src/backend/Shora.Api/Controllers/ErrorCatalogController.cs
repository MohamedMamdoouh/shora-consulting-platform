using Microsoft.AspNetCore.Mvc;
using Shora.Api.Infrastructure;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Contracts.Common;

namespace Shora.Api.Controllers;

[Route("api/v{version:apiVersion}/errors")]
public sealed class ErrorCatalogController : ApiControllerBase
{
    [HttpGet]
    [EndpointName("ErrorCatalog.List")]
    [EndpointSummary("List all documented API error codes")]
    [ProducesResponseType(typeof(ErrorCatalogListResponse), StatusCodes.Status200OK)]
    public IActionResult List()
    {
        Response.Headers.CacheControl = "public, max-age=3600";
        var items = ErrorCatalog.All
            .Where(e => e.Code != ErrorCodes.Errors.NotFound)
            .Select(ToResponse)
            .ToList();
        return Ok(new ErrorCatalogListResponse(items));
    }

    [HttpGet("{code}")]
    [EndpointName("ErrorCatalog.Get")]
    [EndpointSummary("Get documentation for a single API error code")]
    [ProducesResponseType(typeof(ErrorCatalogEntryResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public IActionResult Get(string code)
    {
        Response.Headers.CacheControl = "public, max-age=3600";

        var entry = ErrorCatalog.TryGet(code);
        if (entry is null || entry.Code == ErrorCodes.Errors.NotFound)
        {
            return ToProblem(Error.NotFound(
                ErrorCodes.Errors.NotFound,
                "Error code not found."));
        }

        return Ok(ToResponse(entry));
    }

    private static ErrorCatalogEntryResponse ToResponse(ErrorCatalogEntry entry) =>
        new(
            entry.Code,
            entry.Status,
            entry.Title,
            entry.Summary,
            ApiProblemDetailsMapper.ErrorTypeFor(entry.Code),
            entry.WhenItOccurs,
            entry.RelatedEndpoint);
}
