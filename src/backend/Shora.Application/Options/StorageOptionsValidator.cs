using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class StorageOptionsValidator : IValidateOptions<StorageOptions>
{
    public ValidateOptionsResult Validate(string? name, StorageOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            return ValidateOptionsResult.Fail("Storage:ConnectionString must be configured for production.");
        }

        return ValidateOptionsResult.Success;
    }
}
