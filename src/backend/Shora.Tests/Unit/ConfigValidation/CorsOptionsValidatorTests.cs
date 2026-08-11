using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class CorsOptionsValidatorTests
{
    private readonly CorsOptionsValidator _validator = new();

    [Fact]
    public void Validate_fails_when_no_origins()
    {
        var result = _validator.Validate(null, new CorsOptions { AllowedOrigins = [] });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_for_placeholder_origin()
    {
        var result = _validator.Validate(null, new CorsOptions
        {
            AllowedOrigins = ["https://YOUR_PRODUCTION_HOST"]
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_succeeds_for_valid_https_origins()
    {
        var result = _validator.Validate(null, new CorsOptions
        {
            AllowedOrigins = ["https://shora.example.com"]
        });

        Assert.True(result.Succeeded);
    }
}
