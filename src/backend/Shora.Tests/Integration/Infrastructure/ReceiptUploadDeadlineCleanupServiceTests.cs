using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Bookings;
using Shora.Application.Common;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class ReceiptUploadDeadlineCleanupServiceTests
{
    private readonly SqlServerFixture _sqlServer;

    public ReceiptUploadDeadlineCleanupServiceTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_cancels_expired_pending_payment_hold_and_releases_slot()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = CreateServices(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var (bookingId, slotId) = await SeedExpiredPendingPaymentHoldAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<ReceiptUploadDeadlineCleanupService>();
            var processedCount = await cleanupService.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
            Assert.False(slot.IsBooked);
            Assert.Null(slot.BookingId);

            var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
            Assert.Equal(BookingStatus.Cancelled, booking.Status);
            Assert.Null(booking.AvailabilitySlotId);

            var payment = await context.Payments.AsNoTracking().SingleAsync(p => p.BookingId == bookingId, cancellationToken);
            Assert.Equal(PaymentStatus.Void, payment.Status);

            var audit = await context.BookingStatusAudits
                .AsNoTracking()
                .SingleAsync(a => a.BookingId == bookingId, cancellationToken);
            Assert.Equal(BookingStatus.PendingPayment, audit.FromStatus);
            Assert.Equal(BookingStatus.Cancelled, audit.ToStatus);
            Assert.Equal(AuditActor.System, audit.Actor);
            Assert.Equal("Receipt upload deadline expired", audit.Reason);

            var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
                m => m.AggregateId == bookingId && m.MessageType == OutboxMessageTypes.ClientBookingCancelledEmail,
                cancellationToken);
            Assert.Equal(OutboxMessageStatus.Pending, outbox.Status);
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
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = CreateServices(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);
            await SeedExpiredPendingPaymentHoldAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<ReceiptUploadDeadlineCleanupService>();

            Assert.Equal(1, await cleanupService.RunAsync(cancellationToken));
            Assert.Equal(0, await cleanupService.RunAsync(cancellationToken));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_does_not_touch_pending_approval()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = CreateServices(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);
            var (bookingId, slotId) = await SeedExpiredPendingPaymentHoldAsync(
                services,
                cancellationToken,
                BookingStatus.PendingApproval,
                PaymentStatus.UnderReview,
                receiptUploadDeadlineUtc: null);

            await using var scope = services.CreateAsyncScope();
            var cleanupService = scope.ServiceProvider.GetRequiredService<ReceiptUploadDeadlineCleanupService>();

            Assert.Equal(0, await cleanupService.RunAsync(cancellationToken));

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var slot = await context.AvailabilitySlots.AsNoTracking().SingleAsync(s => s.Id == slotId, cancellationToken);
            Assert.True(slot.IsBooked);

            var booking = await context.Bookings.AsNoTracking().SingleAsync(b => b.Id == bookingId, cancellationToken);
            Assert.Equal(BookingStatus.PendingApproval, booking.Status);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static IServiceProvider CreateServices(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<ICacheInvalidator, NoOpCacheInvalidator>();
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<BookingTransitionHelper>();
        services.AddScoped<ReceiptUploadDeadlineCleanupService>();

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

    private static async Task<(Guid BookingId, Guid SlotId)> SeedExpiredPendingPaymentHoldAsync(
        IServiceProvider services,
        CancellationToken cancellationToken,
        BookingStatus bookingStatus = BookingStatus.PendingPayment,
        PaymentStatus paymentStatus = PaymentStatus.AwaitingReceipt,
        DateTime? receiptUploadDeadlineUtc = null)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTime.UtcNow;

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
            throw new InvalidOperationException(
                $"Failed to create test client: {string.Join(", ", createResult.Errors.Select(error => error.Description))}");
        }

        await userManager.AddToRoleAsync(client, DatabaseSeeder.ClientRole);

        var slot = await context.AvailabilitySlots.FirstAsync(s => !s.IsBooked, cancellationToken);
        var bookingId = Guid.NewGuid();
        slot.IsBooked = true;
        slot.BookingId = bookingId;

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            AvailabilitySlotId = slot.Id,
            SlotStartUtc = slot.StartTimeUtc,
            SlotEndUtc = slot.EndTimeUtc,
            DeliveryMethod = DeliveryMethod.Chat,
            Status = bookingStatus,
            ReceiptUploadDeadlineUtc = receiptUploadDeadlineUtc ?? now.AddMinutes(-5),
            CreatedAt = now.AddHours(-2)
        });

        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Status = paymentStatus,
            Amount = 500m,
            Currency = "EGP",
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        });

        await context.SaveChangesAsync(cancellationToken);
        return (bookingId, slot.Id);
    }

    private sealed class NoOpCacheInvalidator : ICacheInvalidator
    {
        public Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}
