using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return ValidateOptionsResult.Fail("Storage:Endpoint must be configured for production.");
        }

        if (string.IsNullOrWhiteSpace(options.AccessKeyId))
        {
            return ValidateOptionsResult.Fail("Storage:AccessKeyId must be configured for production.");
        }

        if (string.IsNullOrWhiteSpace(options.SecretAccessKey))
        {
            return ValidateOptionsResult.Fail("Storage:SecretAccessKey must be configured for production.");
        }

        return ValidateOptionsResult.Success;
    }
}
