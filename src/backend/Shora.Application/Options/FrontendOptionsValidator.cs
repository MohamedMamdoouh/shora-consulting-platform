using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class FrontendOptionsValidator : IValidateOptions<FrontendOptions>
{
    public ValidateOptionsResult Validate(string? name, FrontendOptions options)
    {
        if (!ProductionConfigValidation.IsValidProductionHttpsUrl(options.BaseUrl))
        {
            return ValidateOptionsResult.Fail(
                "Frontend:BaseUrl must be a valid HTTPS URL and must not use the production placeholder.");
        }

        return ValidateOptionsResult.Success;
    }
}
