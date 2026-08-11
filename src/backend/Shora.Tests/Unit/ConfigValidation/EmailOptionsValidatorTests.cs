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
    }

    [Fact]
    public void Validate_succeeds_when_email_configured()
    {
        var result = _validator.Validate(null, new EmailOptions
        {
            Host = "smtp.example.com",
            FromAddress = "noreply@example.com"
        });

        Assert.True(result.Succeeded);
    }
}
