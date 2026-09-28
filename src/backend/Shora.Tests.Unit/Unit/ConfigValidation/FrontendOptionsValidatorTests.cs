using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class FrontendOptionsValidatorTests
{
    private readonly FrontendOptionsValidator _validator = new();

    [Fact]
    public void Validate_fails_for_placeholder_url()
    {
        var result = _validator.Validate(null, new FrontendOptions
        {
            BaseUrl = "https://YOUR_PRODUCTION_HOST"
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_for_http_url()
    {
        var result = _validator.Validate(null, new FrontendOptions
        {
            BaseUrl = "http://localhost:4200"
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_succeeds_for_https_production_url()
    {
        var result = _validator.Validate(null, new FrontendOptions
        {
            BaseUrl = "https://shora.example.com"
        });

        Assert.True(result.Succeeded);
    }
}
