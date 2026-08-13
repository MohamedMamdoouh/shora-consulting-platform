using System.Net.Mail;
using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class EmailOptionsValidator : IValidateOptions<EmailOptions>
{
    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Fail(
                "Email:ApiKey and Email:FromAddress must be configured for production.");
        }

        if (!MailAddress.TryCreate(options.FromAddress.Trim(), out _))
        {
            return ValidateOptionsResult.Fail(
                "Email:FromAddress must be a valid email address.");
        }

        return ValidateOptionsResult.Success;
    }
}
