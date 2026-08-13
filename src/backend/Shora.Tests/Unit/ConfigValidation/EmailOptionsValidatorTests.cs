using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class EmailOptionsValidatorTests
{
    private readonly EmailOptionsValidator _validator = new();

    [Fact]
    public void Validate_fails_when_email_not_configured()
    {
        var result = _validator.Validate(null, new EmailOptions());

        Assert.False(result.Succeeded);
        Assert.Contains("Email:ApiKey and Email:FromAddress", result.FailureMessage, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_succeeds_when_email_configured()
    {
        var result = _validator.Validate(null, new EmailOptions
        {
            ApiKey = "xkeysib-test",
            FromAddress = "you@gmail.com"
        });

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_from_address_is_invalid()
    {
        var result = _validator.Validate(null, new EmailOptions
        {
            ApiKey = "xkeysib-test",
            FromAddress = "not-an-email"
        });

        Assert.False(result.Succeeded);
        Assert.Contains("valid email address", result.FailureMessage, StringComparison.OrdinalIgnoreCase);
    }
}
