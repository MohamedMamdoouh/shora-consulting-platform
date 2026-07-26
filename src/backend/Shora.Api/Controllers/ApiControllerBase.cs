using Asp.Versioning;
using Microsoft.AspNetCore.Mvc;
using Shora.Api.Infrastructure;
using Shora.Application.Common.Results;

namespace Shora.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
public abstract class ApiControllerBase : ControllerBase
{
    protected IActionResult FromResult(Result result)
    {
        if (result.IsSuccess)
        {
            return Ok();
        }

        return ToProblem(result.Error!);
    }

    protected IActionResult FromResult<T>(Result<T> result)
    {
        if (result.IsSuccess)
        {
            return Ok(result.Value);
        }

        return ToProblem(result.Error!);
    }

    protected IActionResult ToProblem(Error error)
    {
        var problem = ApiProblemDetailsMapper.FromError(error, HttpContext);
        return new ObjectResult(problem) { StatusCode = error.StatusCode };
    }
}
