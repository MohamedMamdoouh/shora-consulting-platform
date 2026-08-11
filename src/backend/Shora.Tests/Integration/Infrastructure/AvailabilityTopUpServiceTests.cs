using Microsoft.AspNetCore.Identity;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Availability;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Postgres")]
public class AvailabilityTopUpServiceTests
{
    private readonly PostgresFixture _sqlServer;

    public AvailabilityTopUpServiceTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_extends_horizon_when_time_advances()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var seedTime = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var advancedTime = seedTime.AddDays(7);
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = CreateServices(connectionString, seedTime);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            await using (var scope = services.CreateAsyncScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var horizonCutoff = seedTime.AddDays(SlotGenerationConstants.HorizonWeeks * 7 - 7);

                var staleSlots = await context.AvailabilitySlots
                    .Where(slot => slot.StartTimeUtc >= horizonCutoff)
                    .ToListAsync(cancellationToken);

                context.AvailabilitySlots.RemoveRange(staleSlots);
                await context.SaveChangesAsync(cancellationToken);

                var countBeforeAdvance = await context.AvailabilitySlots.CountAsync(cancellationToken);
                Assert.True(countBeforeAdvance > 0);
            }

            ReplaceDateTimeProvider(services, advancedTime);

            await using var runScope = services.CreateAsyncScope();
            var topUpService = runScope.ServiceProvider.GetRequiredService<AvailabilityTopUpService>();
            await topUpService.RunAsync(cancellationToken);

            var contextAfter = runScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var expectedHorizonEnd = advancedTime.AddDays(SlotGenerationConstants.HorizonWeeks * 7);
            var maxStartUtc = await contextAfter.AvailabilitySlots
                .MaxAsync(slot => slot.StartTimeUtc, cancellationToken);

            Assert.True(maxStartUtc >= expectedHorizonEnd.AddDays(-2));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_invalidates_availability_cache()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var cacheInvalidator = new RecordingCacheInvalidator();
        var services = CreateServices(connectionString, fixedNow, cacheInvalidator);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var topUpService = scope.ServiceProvider.GetRequiredService<AvailabilityTopUpService>();
            await topUpService.RunAsync(cancellationToken);

            Assert.Equal(1, cacheInvalidator.AvailabilityInvalidationCount);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static IServiceProvider CreateServices(
        string connectionString,
        DateTime utcNow,
        ICacheInvalidator? cacheInvalidator = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<AvailabilityTopUpService>();

        if (cacheInvalidator is null)
        {
            services.AddScoped<ICacheInvalidator, NoOpCacheInvalidator>();
        }
        else
        {
            services.AddSingleton(cacheInvalidator);
            services.AddSingleton<ICacheInvalidator>(cacheInvalidator);
        }

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

    private static void ReplaceDateTimeProvider(IServiceProvider services, DateTime utcNow)
    {
        var descriptor = services.GetRequiredService<IDateTimeProvider>();
        if (descriptor is FixedDateTimeProvider fixedProvider)
        {
            fixedProvider.SetUtcNow(utcNow);
        }
    }

    private sealed class FixedDateTimeProvider : IDateTimeProvider
    {
        private DateTime _utcNow;

        public FixedDateTimeProvider(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;

        public void SetUtcNow(DateTime utcNow) => _utcNow = utcNow;
    }

    private sealed class RecordingCacheInvalidator : ICacheInvalidator
    {
        public int AvailabilityInvalidationCount { get; private set; }

        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default)
        {
            AvailabilityInvalidationCount++;
            return Task.CompletedTask;
        }

        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class NoOpCacheInvalidator : ICacheInvalidator
    {
        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
