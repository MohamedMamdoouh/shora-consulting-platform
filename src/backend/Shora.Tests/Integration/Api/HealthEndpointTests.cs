using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("SqlServer")]
public class HealthEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly SqlServerFixture _sqlServer;
    private readonly string _databaseName;

    public HealthEndpointTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
        var connectionString = sqlServer.CreateDatabaseAsync().GetAwaiter().GetResult();
        _databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog
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
    public async Task Get_health_returns_ok()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _sqlServer.DropDatabaseAsync(_databaseName).GetAwaiter().GetResult();
    }
}
