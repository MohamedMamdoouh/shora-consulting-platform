using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class JobHeartbeatServiceTests
{
    private readonly SqlServerFixture _sqlServer;

    public JobHeartbeatServiceTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RecordSuccessAsync_creates_and_updates_last_success_timestamp()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var heartbeatService = scope.ServiceProvider.GetRequiredService<JobHeartbeatService>();
            var dateTimeProvider = (FixedDateTimeProvider)scope.ServiceProvider.GetRequiredService<IDateTimeProvider>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await heartbeatService.RecordSuccessAsync(
                BackgroundJobNames.ReceiptUploadDeadlineCleanup,
                cancellationToken);

            var firstSuccess = await heartbeatService.GetLastSuccessAtUtcAsync(
                BackgroundJobNames.ReceiptUploadDeadlineCleanup,
                cancellationToken);

            Assert.Equal(fixedNow, firstSuccess);

            var entry = await context.JobRunHistories
                .SingleAsync(j => j.JobName == BackgroundJobNames.ReceiptUploadDeadlineCleanup, cancellationToken);

            Assert.Equal(fixedNow, entry.LastSuccessAtUtc);
            Assert.Null(entry.LastFailureAtUtc);

            dateTimeProvider.Advance(TimeSpan.FromMinutes(5));

            await heartbeatService.RecordSuccessAsync(
                BackgroundJobNames.ReceiptUploadDeadlineCleanup,
                cancellationToken);

            var secondSuccess = await heartbeatService.GetLastSuccessAtUtcAsync(
                BackgroundJobNames.ReceiptUploadDeadlineCleanup,
                cancellationToken);

            Assert.Equal(fixedNow.AddMinutes(5), secondSuccess);
            Assert.True(secondSuccess > firstSuccess);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RecordFailureAsync_persists_failure_details()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = CreateServices(connectionString, DateTime.UtcNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var heartbeatService = scope.ServiceProvider.GetRequiredService<JobHeartbeatService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            await heartbeatService.RecordFailureAsync(
                BackgroundJobNames.TempBlobCleanup,
                "Blob storage unavailable",
                cancellationToken);

            var entry = await context.JobRunHistories
                .SingleAsync(j => j.JobName == BackgroundJobNames.TempBlobCleanup, cancellationToken);

            Assert.Null(entry.LastSuccessAtUtc);
            Assert.NotNull(entry.LastFailureAtUtc);
            Assert.Equal("Blob storage unavailable", entry.LastError);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static IServiceProvider CreateServices(string connectionString, DateTime utcNow)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddScoped<JobHeartbeatService>();
        services.AddScoped<SlotGenerationService>();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AdminSeed:Email"] = "admin@test.local",
                ["AdminSeed:Password"] = "TestPass123!",
                ["Seed:ConsultantWhatsAppNumber"] = "+201012345678",
                ["Seed:VodafoneCashNumber"] = "01012345678",
                ["Seed:InstaPayHandle"] = "test@instapay"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));

        return services.BuildServiceProvider();
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        private DateTime _utcNow = utcNow;

        public DateTime UtcNow => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow = _utcNow.Add(duration);
    }
}
