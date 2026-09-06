using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class StorageOptionsValidatorTests
{
    private readonly StorageOptionsValidator _validator = new();

    [Fact]
    public void Validate_fails_when_endpoint_empty()
    {
        var result = _validator.Validate(null, new StorageOptions());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_access_key_empty()
    {
        var result = _validator.Validate(null, new StorageOptions
        {
            Endpoint = "http://localhost:9000"
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_fails_when_secret_access_key_empty()
    {
        var result = _validator.Validate(null, new StorageOptions
        {
            Endpoint = "http://localhost:9000",
            AccessKeyId = "minioadmin"
        });

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_succeeds_when_all_required_fields_set()
    {
        var result = _validator.Validate(null, new StorageOptions
        {
            Endpoint = "http://localhost:9000",
            AccessKeyId = "minioadmin",
            SecretAccessKey = "minioadmin"
        });

        Assert.True(result.Succeeded);
    }
}
