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
public class CancellationRequestAutoDeclineServiceTests
{
    private readonly SqlServerFixture _sqlServer;

    public CancellationRequestAutoDeclineServiceTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_auto_declines_pending_request_and_returns_booking_to_confirmed()
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

            var bookingId = await SeedPendingCancellationRequestAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<CancellationRequestAutoDeclineService>();
            var processedCount = await service.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var booking = await context.Bookings
                .AsNoTracking()
                .Include(b => b.CancellationRequest)
                .SingleAsync(b => b.Id == bookingId, cancellationToken);

            Assert.Equal(BookingStatus.Confirmed, booking.Status);
            Assert.NotNull(booking.CancellationRequest);
            Assert.Equal(CancellationRequestStatus.AutoDeclined, booking.CancellationRequest!.Status);
            Assert.Equal(DecisionReasonCode.Policy, booking.CancellationRequest.DecisionReasonCode);
            Assert.Equal(fixedNow, booking.CancellationRequest.ReviewedAtUtc);

            var audit = await context.BookingStatusAudits
                .AsNoTracking()
                .SingleAsync(a => a.BookingId == bookingId, cancellationToken);
            Assert.Equal(BookingStatus.CancellationRequested, audit.FromStatus);
            Assert.Equal(BookingStatus.Confirmed, audit.ToStatus);
            Assert.Equal(AuditActor.System, audit.Actor);

            var outbox = await context.OutboxMessages.AsNoTracking().SingleAsync(
                m => m.AggregateId == bookingId
                     && m.MessageType == OutboxMessageTypes.ClientCancellationRequestDeclinedEmail,
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
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);
            await SeedPendingCancellationRequestAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<CancellationRequestAutoDeclineService>();

            Assert.Equal(1, await service.RunAsync(cancellationToken));
            Assert.Equal(0, await service.RunAsync(cancellationToken));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<Guid> SeedPendingCancellationRequestAsync(
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

        var slotStart = now.AddMinutes(30);
        var slotEnd = slotStart.AddHours(1);
        var bookingId = Guid.NewGuid();
        var requestId = Guid.NewGuid();

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotEnd,
            DeliveryMethod = DeliveryMethod.Chat,
            ContactPhone = "+201012345678",
            Status = BookingStatus.CancellationRequested,
            CreatedAt = now.AddDays(-1)
        });

        context.CancellationRequests.Add(new CancellationRequest
        {
            Id = requestId,
            BookingId = bookingId,
            RequestedByClientId = clientId,
            RequestedAtUtc = now.AddHours(-2),
            ClientReason = "Cannot attend",
            AutoDeclineAtUtc = slotStart.AddHours(-1),
            Status = CancellationRequestStatus.Pending
        });

        context.Payments.Add(new Payment
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            Amount = 500m,
            Currency = "EGP",
            Status = PaymentStatus.Approved,
            CreatedAt = now.AddDays(-1),
            UpdatedAt = now.AddDays(-1)
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private static IServiceProvider CreateServices(string connectionString, DateTime utcNow)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<BookingTransitionHelper>();
        services.AddScoped<CancellationRequestAutoDeclineService>();

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
}
