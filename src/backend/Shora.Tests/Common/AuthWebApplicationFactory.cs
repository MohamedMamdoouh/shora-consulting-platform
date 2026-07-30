using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;

namespace Shora.Tests.Common;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqlServerFixture _sqlServer;
    private readonly string _connectionString;
    private readonly string _databaseName;
    private bool _initialized;

    public AuthWebApplicationFactory(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
        _connectionString = sqlServer.CreateDatabaseAsync().GetAwaiter().GetResult();
        _databaseName = new SqlConnectionStringBuilder(_connectionString).InitialCatalog
            ?? throw new InvalidOperationException("Test database name is missing from the connection string.");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:DefaultConnection", _connectionString);
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-min-32-characters-long!");
        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Issuer"] = "Shora",
                ["Jwt:Audience"] = "Shora.Web",
                ["Jwt:SigningKey"] = "test-signing-key-min-32-characters-long!",
                ["Jwt:AccessTokenMinutes"] = "15",
                ["Jwt:RefreshTokenDays"] = "7",
                ["Frontend:BaseUrl"] = "http://localhost:4200",
                ["Cors:AllowedOrigins:0"] = "http://localhost:4200",
                ["AdminSeed:Email"] = "admin@test.local",
                ["AdminSeed:Password"] = "TestPass123!",
                ["Google:ClientId"] = "test-google-client-id",
                ["Cache:Enabled"] = "true",
                ["Cache:SettingsPublicTtlSeconds"] = "300",
                ["Cache:AvailabilityTtlSeconds"] = "30",
                ["BackgroundJobs:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            services.AddScoped<IGoogleTokenValidator, FakeGoogleTokenValidator>();
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        EnsureInitialized(host.Services);
        return host;
    }

    private void EnsureInitialized(IServiceProvider services)
    {
        if (_initialized)
        {
            return;
        }

        TestDatabaseInitializer.MigrateAndSeedAsync(services).GetAwaiter().GetResult();
        _initialized = true;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _sqlServer.DropDatabaseAsync(_databaseName).GetAwaiter().GetResult();
        }

        base.Dispose(disposing);
    }
}

public sealed class FakeGoogleTokenValidator : IGoogleTokenValidator
{
    public Task<GoogleTokenPayload?> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        if (idToken == "invalid")
        {
            return Task.FromResult<GoogleTokenPayload?>(null);
        }

        if (idToken.StartsWith("email:", StringComparison.Ordinal))
        {
            var email = idToken["email:".Length..];
            return Task.FromResult<GoogleTokenPayload?>(
                new GoogleTokenPayload(email, "Google User", Guid.NewGuid().ToString()));
        }

        return Task.FromResult<GoogleTokenPayload?>(
            new GoogleTokenPayload($"{idToken}@google.test", "Google User", idToken));
    }
}
