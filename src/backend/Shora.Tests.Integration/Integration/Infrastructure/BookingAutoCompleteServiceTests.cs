using Microsoft.AspNetCore.Identity;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Postgres")]
public class BookingAutoCompleteServiceTests
{
    private readonly PostgresFixture _sqlServer;

    public BookingAutoCompleteServiceTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_completes_confirmed_booking_after_slot_end_and_releases_slot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var fixedNow = new DateTime(2026, 8, 6, 15, 0, 0, DateTimeKind.Utc);
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var (bookingId, slotId) = await SeedConfirmedPastSessionAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<BookingAutoCompleteService>();
            var processedCount = await service.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(
                s => s.Id == slotId,
                cancellationToken);
            Assert.False(slot.IsBooked);
            Assert.Null(slot.BookingId);

            var booking = await context.Bookings.AsNoTracking().SingleAsync(
                b => b.Id == bookingId,
                cancellationToken);
            Assert.Equal(BookingStatus.Completed, booking.Status);
            Assert.Null(booking.AvailabilitySlotId);

            var audit = await context.BookingStatusAudits
                .AsNoTracking()
                .SingleAsync(a => a.BookingId == bookingId, cancellationToken);
            Assert.Equal(BookingStatus.Confirmed, audit.FromStatus);
            Assert.Equal(BookingStatus.Completed, audit.ToStatus);
            Assert.Equal(AuditActor.System, audit.Actor);
            Assert.Equal("Session ended", audit.Reason);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_is_idempotent_on_second_run()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var fixedNow = new DateTime(2026, 8, 6, 15, 0, 0, DateTimeKind.Utc);
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);
            await SeedConfirmedPastSessionAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<BookingAutoCompleteService>();

            Assert.Equal(1, await service.RunAsync(cancellationToken));
            Assert.Equal(0, await service.RunAsync(cancellationToken));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<(Guid BookingId, Guid SlotId)> SeedConfirmedPastSessionAsync(
        IServiceProvider services,
        DateTime now,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var clientId = Guid.NewGuid();
        var clientEmail = $"client-{clientId:N}@test.local";
        var client = new ApplicationUser
        {
            Id = clientId,
            UserName = clientEmail,
            Email = clientEmail,
            EmailConfirmed = true,
            DisplayName = "Test Client",
            Role = UserRole.Client
        };

        var createResult = await userManager.CreateAsync(client, "Password123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        await userManager.AddToRoleAsync(client, DatabaseSeeder.ClientRole);

        var slot = await context.AvailabilitySlots.FirstAsync(s => !s.IsBooked, cancellationToken);
        var bookingId = Guid.NewGuid();
        var slotStart = now.AddHours(-2);
        var slotEnd = now.AddMinutes(-30);

        slot.IsBooked = true;
        slot.BookingId = bookingId;
        slot.StartTimeUtc = slotStart;
        slot.EndTimeUtc = slotEnd;

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            AvailabilitySlotId = slot.Id,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotEnd,
            DeliveryMethod = DeliveryMethod.Chat,
            ContactPhone = "+201012345678",
            Status = BookingStatus.Confirmed,
            CreatedAt = slotStart.AddDays(-1)
        });

        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Amount = 500m,
            Currency = "EGP",
            Status = PaymentStatus.Approved,
            CreatedAt = slotStart.AddDays(-1),
            UpdatedAt = slotStart.AddDays(-1)
        });

        await context.SaveChangesAsync(cancellationToken);
        return (bookingId, slot.Id);
    }

    private static IServiceProvider CreateServices(string connectionString, DateTime utcNow)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddSingleton<ICacheInvalidator, NoOpCacheInvalidator>();
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<BookingTransitionHelper>();
        services.AddScoped<BookingAutoCompleteService>();

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
        public DateTime UtcNow => utcNow;
    }

    private sealed class NoOpCacheInvalidator : ICacheInvalidator
    {
        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
