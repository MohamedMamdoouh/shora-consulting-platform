using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Shora.Api.Middleware;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public class HealthEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgresFixture _sqlServer;
    private readonly string _databaseName;

    public HealthEndpointTests(PostgresFixture sqlServer)
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
    public async Task Get_health_returns_ok()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_health_returns_healthy_status_when_database_is_available()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health", cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("healthy", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Get_health_returns_generated_correlation_id_when_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/health", cancellationToken);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.False(string.IsNullOrWhiteSpace(values.Single()));
    }

    [Fact]
    public async Task Get_health_propagates_request_correlation_id()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        const string correlationId = "test-correlation-id-12345";
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, correlationId);

        var response = await client.GetAsync("/api/v1/health", cancellationToken);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        Assert.Equal(correlationId, values.Single());
    }

    [Fact]
    public async Task Get_health_replaces_invalid_correlation_id_with_generated_value()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        const string invalidCorrelationId = "not-valid!@#";
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, invalidCorrelationId);

        var response = await client.GetAsync("/api/v1/health", cancellationToken);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        var returned = values.Single();
        Assert.NotEqual(invalidCorrelationId, returned);
        Assert.Matches("^[a-f0-9]{32}$", returned);
    }

    [Fact]
    public async Task Get_health_replaces_overly_long_correlation_id_with_generated_value()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();
        var tooLong = new string('a', 65);
        client.DefaultRequestHeaders.Add(CorrelationIdMiddleware.HeaderName, tooLong);

        var response = await client.GetAsync("/api/v1/health", cancellationToken);

        Assert.True(response.Headers.TryGetValues(CorrelationIdMiddleware.HeaderName, out var values));
        var returned = values.Single();
        Assert.NotEqual(tooLong, returned);
        Assert.Matches("^[a-f0-9]{32}$", returned);
    }

    [Fact]
    public async Task Get_health_with_railway_healthcheck_host_returns_ok_when_allowed()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = _sqlServer.CreateDatabaseAsync().GetAwaiter().GetResult();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database
            ?? throw new InvalidOperationException("Test database name is missing from the connection string.");

        using var factory = CreateProductionFactory(connectionString);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/health");
        request.Headers.Host = "healthcheck.railway.app";

        var response = await client.SendAsync(request, cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        await _sqlServer.DropDatabaseAsync(databaseName);
    }

    private static WebApplicationFactory<Program> CreateProductionFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(WebHostDefaults.EnvironmentKey, Environments.Production);
            builder.UseSetting("ConnectionStrings:DefaultConnection", connectionString);
            builder.UseSetting("AllowedHosts", "mahmoudelbanna.up.railway.app;healthcheck.railway.app");
            builder.UseSetting("Jwt:SigningKey", "test-signing-key-min-32-characters-long!");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Jwt:Issuer"] = "Shora",
                    ["Jwt:Audience"] = "Shora.Web",
                    ["Jwt:SigningKey"] = "test-signing-key-min-32-characters-long!",
                    ["Frontend:BaseUrl"] = "https://mahmoudelbanna.up.railway.app",
                    ["Cors:AllowedOrigins:0"] = "https://mahmoudelbanna.up.railway.app",
                    ["Email:ApiKey"] = "xkeysib-test",
                    ["Email:FromAddress"] = "noreply@example.com",
                    ["Storage:ConnectionString"] = "UseDevelopmentStorage=true",
                    ["Storage:ReceiptContainer"] = "receipts",
                    ["BackgroundJobs:Enabled"] = "false"
                });
            });
        });

    public void Dispose()
    {
        _factory.Dispose();
        _sqlServer.DropDatabaseAsync(_databaseName).GetAwaiter().GetResult();
    }
}
