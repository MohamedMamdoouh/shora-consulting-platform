using Microsoft.AspNetCore.Mvc;
using Shora.Application.Common;
using Shora.Application.Common.Results;

namespace Shora.Api.Infrastructure;

public static class ApiProblemDetailsMapper
{
    public const string ErrorTypeBase = "https://shora.dev/errors";

    public static string ErrorTypeFor(string code) => $"{ErrorTypeBase}/{code}";

    public static ProblemDetails FromError(Error error, HttpContext httpContext)
    {
        var problem = new ProblemDetails
        {
            Type = ErrorTypeFor(error.Code),
            Title = error.Message,
            Status = error.StatusCode,
            Detail = error.Message,
            Instance = httpContext.Request.Path
        };

        problem.Extensions.TryAdd("code", error.Code);
        return problem;
    }

    public static ValidationProblemDetails FromValidationErrors(
        IDictionary<string, string[]> errors,
        HttpContext httpContext)
    {
        var problem = new ValidationProblemDetails(errors)
        {
            Type = ErrorTypeFor(ErrorCodes.General.Validation),
            Title = "One or more validation errors occurred.",
            Status = StatusCodes.Status400BadRequest,
            Instance = httpContext.Request.Path
        };

        problem.Extensions.TryAdd("code", ErrorCodes.General.Validation);
        return problem;
    }
}
