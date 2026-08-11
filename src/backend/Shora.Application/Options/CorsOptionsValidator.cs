using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class CorsOptionsValidator : IValidateOptions<CorsOptions>
{
    public ValidateOptionsResult Validate(string? name, CorsOptions options)
    {
        var origins = options.AllowedOrigins;
        if (origins is not { Length: > 0 })
        {
            return ValidateOptionsResult.Fail("Cors:AllowedOrigins must contain at least one origin.");
        }

        foreach (var origin in origins)
        {
            if (!ProductionConfigValidation.IsValidProductionHttpsUrl(origin))
            {
                return ValidateOptionsResult.Fail(
                    $"Cors origin '{origin}' must be a valid HTTPS URL and must not use the production placeholder.");
            }
        }

        return ValidateOptionsResult.Success;
    }
}
