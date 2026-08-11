using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Shora.Contracts.Settings;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public class SettingsPublicEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgresFixture _sqlServer;
    private readonly string _databaseName;

    public SettingsPublicEndpointTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
        var connectionString = sqlServer.CreateDatabaseAsync().GetAwaiter().GetResult();
        _databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database
            ?? throw new InvalidOperationException("Test database name is missing from the connection string.");

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "Shora",
                    ["Jwt:Audience"] = "Shora.Web",
                    ["Jwt:SigningKey"] = "test-signing-key-min-32-characters-long!",
                    ["Cors:AllowedOrigins:0"] = "http://localhost:4200"
                });
            });
            builder.ConfigureServices(_ => { });
        });
    }

    [Fact]
    public async Task Get_public_settings_returns_price_and_duration()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/settings/public");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<PublicSettingsResponse>();
        Assert.NotNull(body);
        Assert.True(body.SessionPrice > 0);
        Assert.True(body.SessionDurationMinutes > 0);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _sqlServer.DropDatabaseAsync(_databaseName).GetAwaiter().GetResult();
    }
}
