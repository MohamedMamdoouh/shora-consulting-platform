using Microsoft.Extensions.Options;

namespace Shora.Application.Options;

public sealed class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public const int MinSigningKeyLength = 32;

    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SigningKey))
        {
            return ValidateOptionsResult.Fail("Jwt:SigningKey is not configured.");
        }

        if (options.SigningKey.Length < MinSigningKeyLength)
        {
            return ValidateOptionsResult.Fail(
                $"Jwt:SigningKey must be at least {MinSigningKeyLength} characters.");
        }

        return ValidateOptionsResult.Success;
    }
}
