using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (options.IsConfigured)
        {
            return ValidateOptionsResult.Success;
        }

        return ValidateOptionsResult.Fail(
            "Email:ApiKey and Email:FromAddress must be configured for production.");
    }
}
