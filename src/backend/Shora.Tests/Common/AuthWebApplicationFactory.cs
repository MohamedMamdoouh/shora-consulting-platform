using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;

namespace Shora.Tests.Common;

public sealed class AuthWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly PostgresFixture _sqlServer;
    private readonly string _connectionString;
    private readonly string _databaseName;
    private bool _initialized;

    public AuthWebApplicationFactory(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
        _connectionString = sqlServer.CreateDatabaseAsync().GetAwaiter().GetResult();
        _databaseName = new NpgsqlConnectionStringBuilder(_connectionString).Database
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
                ["Cache:Enabled"] = "true",
                ["Cache:SettingsPublicTtlSeconds"] = "300",
                ["Cache:AvailabilityTtlSeconds"] = "30",
                ["BackgroundJobs:Enabled"] = "false"
            });
        });

        builder.ConfigureServices(services =>
        {
            var fileStorageDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IFileStorage));
            if (fileStorageDescriptor is not null)
            {
                services.Remove(fileStorageDescriptor);
            }

            services.AddSingleton<IFileStorage, InMemoryFileStorage>();

            var malwareScannerDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IMalwareScanner));
            if (malwareScannerDescriptor is not null)
            {
                services.Remove(malwareScannerDescriptor);
            }

            services.AddSingleton<TestMalwareScanner>();
            services.AddSingleton<IMalwareScanner>(sp => sp.GetRequiredService<TestMalwareScanner>());
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
