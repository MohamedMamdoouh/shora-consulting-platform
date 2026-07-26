using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Options;
using Shora.Api.Infrastructure;
using Shora.Application.Common;
using Shora.Application.Common.Results;
using Shora.Application.Options;

namespace Shora.Api.Filters;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class ValidateAuthCookieOriginAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var corsOptions = context.HttpContext.RequestServices
            .GetRequiredService<IOptions<CorsOptions>>().Value;

        var origin = context.HttpContext.Request.Headers.Origin.ToString();
        var referer = context.HttpContext.Request.Headers.Referer.ToString();

        if (string.IsNullOrEmpty(origin) && string.IsNullOrEmpty(referer))
        {
            return;
        }

        var allowed = corsOptions.EffectiveOrigins;
        if (!string.IsNullOrEmpty(origin) && allowed.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            return;
        }

        if (!string.IsNullOrEmpty(referer) &&
            allowed.Any(o => referer.StartsWith(o, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        var error = Error.Forbidden(ErrorCodes.General.Forbidden, "Origin is not allowed.");
        var problem = ApiProblemDetailsMapper.FromError(error, context.HttpContext);
        context.Result = new ObjectResult(problem) { StatusCode = error.StatusCode };
    }
}
