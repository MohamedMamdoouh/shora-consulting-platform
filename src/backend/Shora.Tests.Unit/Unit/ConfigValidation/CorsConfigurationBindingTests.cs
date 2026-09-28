using Microsoft.Extensions.Configuration;
using Shora.Application.Options;

namespace Shora.Tests.Unit.ConfigValidation;

public class CorsConfigurationBindingTests
{
    [Fact]
    public void Production_json_wins_when_no_env_override()
    {
        var config = BuildProductionConfig();
        var cors = BindCorsOptions(config);

        Assert.Equal(["https://YOUR_PRODUCTION_HOST.onrender.com"], cors.AllowedOrigins);
    }

    [Fact]
    public void Env_var_localhost_overrides_production_json_index_zero()
    {
        var config = BuildProductionConfig(new Dictionary<string, string?>
        {
            ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
        });
        var cors = BindCorsOptions(config);

        Assert.Equal(["http://localhost:4200"], cors.AllowedOrigins);
    }

    [Fact]
    public void Empty_allowed_origins_uses_effective_origins_fallback_in_dev()
    {
        var cors = new CorsOptions();

        Assert.Equal(["http://localhost:4200"], cors.EffectiveOrigins);
    }

    [Fact]
    public void Missing_cors_section_keeps_empty_allowed_origins()
    {
        var config = new ConfigurationBuilder().Build();
        var cors = BindCorsOptions(config);

        Assert.Empty(cors.AllowedOrigins);
    }

    private static IConfiguration BuildProductionConfig(
        Dictionary<string, string?>? extraEnv = null)
    {
        var apiDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "Shora.Api"));

        var builder = new ConfigurationBuilder()
            .SetBasePath(apiDir)
            .AddJsonFile("appsettings.json", optional: false)
            .AddJsonFile("appsettings.Production.json", optional: false);

        if (extraEnv is not null)
        {
            builder.AddInMemoryCollection(extraEnv);
        }

        return builder.Build();
    }

    private static CorsOptions BindCorsOptions(IConfiguration config)
    {
        var cors = new CorsOptions();
        config.GetSection(CorsOptions.SectionName).Bind(cors);
        return cors;
    }
}
