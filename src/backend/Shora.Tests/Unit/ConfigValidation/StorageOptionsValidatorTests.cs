using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class StorageOptionsValidatorTests
{
    private readonly StorageOptionsValidator _validator = new();

    [Fact]
    public void Validate_fails_when_connection_string_empty()
    {
        var result = _validator.Validate(null, new StorageOptions());

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Validate_succeeds_when_connection_string_set()
    {
        var result = _validator.Validate(null, new StorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true"
        });

        Assert.True(result.Succeeded);
    }
}
