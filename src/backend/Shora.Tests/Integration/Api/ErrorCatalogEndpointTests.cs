using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Shora.Api.Infrastructure;
using Shora.Application.Common;
using Shora.Contracts.Common;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Api;

[Collection("Postgres")]
public sealed class ErrorCatalogEndpointTests : IDisposable
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly PostgresFixture _sqlServer;
    private readonly string _databaseName;

    public ErrorCatalogEndpointTests(PostgresFixture sqlServer)
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
        });
    }

    [Fact]
    public async Task Get_error_by_code_returns_catalog_entry()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/errors/auth.duplicate_email", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorCatalogEntryResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Equal(ErrorCodes.Auth.DuplicateEmail, body!.Code);
        Assert.Equal(409, body.Status);
        Assert.Equal("Email is already registered", body.Title);
        Assert.Equal(ApiProblemDetailsMapper.ErrorTypeFor(ErrorCodes.Auth.DuplicateEmail), body.Type);
    }

    [Fact]
    public async Task Get_unknown_error_code_returns_not_found()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/errors/not.a.real.code", cancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);

        var problemJson = await response.Content.ReadFromJsonAsync<JsonElement>(cancellationToken);
        Assert.Equal(ErrorCodes.Errors.NotFound, problemJson.GetProperty("code").GetString());
    }

    [Fact]
    public async Task List_errors_includes_duplicate_email()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/errors", cancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<ErrorCatalogListResponse>(cancellationToken);
        Assert.NotNull(body);
        Assert.Contains(body!.Items, e => e.Code == ErrorCodes.Auth.DuplicateEmail);
        Assert.DoesNotContain(body.Items, e => e.Code == ErrorCodes.Errors.NotFound);
    }

    public void Dispose()
    {
        _factory.Dispose();
        _sqlServer.DropDatabaseAsync(_databaseName).GetAwaiter().GetResult();
    }
}
