using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class JwtOptionsValidatorTests
{
    private readonly JwtOptionsValidator _validator = new();

    [Fact]
    public void Validate_fails_when_signing_key_empty()
    {
        var result = _validator.Validate(null, new JwtOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("SigningKey", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_fails_when_signing_key_too_short()
    {
        var result = _validator.Validate(null, new JwtOptions { SigningKey = "too-short" });

        Assert.False(result.Succeeded);
        Assert.Contains("32", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_succeeds_when_signing_key_meets_minimum_length()
    {
        var result = _validator.Validate(null, new JwtOptions
        {
            SigningKey = "test-signing-key-min-32-characters-long!"
        });

        Assert.True(result.Succeeded);
    }
}
