using Microsoft.AspNetCore.Identity;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Ops;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Postgres")]
public class OpsMonitoringServiceTests
{
    private readonly PostgresFixture _sqlServer;

    public OpsMonitoringServiceTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task EvaluateAlertsAsync_flags_stale_pending_approval_booking()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTime(2026, 8, 6, 18, 0, 0, DateTimeKind.Utc);
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var bookingId = await SeedPendingApprovalBookingAsync(
                services,
                fixedNow.AddHours(-7),
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<OpsMonitoringService>();
            var alerts = await service.EvaluateAlertsAsync(cancellationToken);

            Assert.Contains(
                alerts,
                alert => alert.Kind == OpsAlertKind.PendingApprovalBacklog
                         && alert.Severity == OpsAlertSeverity.Warning
                         && alert.Context["bookingId"] == bookingId.ToString());
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task EvaluateAlertsAsync_flags_stale_job_heartbeat()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTime(2026, 8, 6, 18, 0, 0, DateTimeKind.Utc);
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.JobRunHistories.Add(new JobRunHistory
            {
                JobName = BackgroundJobNames.OutboxDispatcher,
                LastSuccessAtUtc = fixedNow.AddMinutes(-3)
            });
            await context.SaveChangesAsync(cancellationToken);

            var service = scope.ServiceProvider.GetRequiredService<OpsMonitoringService>();
            var alerts = await service.EvaluateAlertsAsync(cancellationToken);

            Assert.Contains(
                alerts,
                alert => alert.Kind == OpsAlertKind.JobHeartbeatStale
                         && alert.Severity == OpsAlertSeverity.Warning
                         && alert.Context["jobName"] == BackgroundJobNames.OutboxDispatcher);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task EvaluateAlertsAsync_skips_stale_job_heartbeat_during_startup_grace()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTime(2026, 8, 6, 18, 0, 0, DateTimeKind.Utc);
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = CreateServices(connectionString, fixedNow, startedAtUtc: fixedNow.AddMinutes(-5));

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.JobRunHistories.Add(new JobRunHistory
            {
                JobName = BackgroundJobNames.OutboxDispatcher,
                LastSuccessAtUtc = fixedNow.AddMinutes(-3)
            });
            await context.SaveChangesAsync(cancellationToken);

            var service = scope.ServiceProvider.GetRequiredService<OpsMonitoringService>();
            var alerts = await service.EvaluateAlertsAsync(cancellationToken);

            Assert.DoesNotContain(
                alerts,
                alert => alert.Kind == OpsAlertKind.JobHeartbeatStale);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task EvaluateAlertsAsync_flags_dead_lettered_outbox_message()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fixedNow = new DateTime(2026, 8, 6, 18, 0, 0, DateTimeKind.Utc);
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var services = CreateServices(connectionString, fixedNow);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            var messageId = Guid.NewGuid();
            await using var scope = services.CreateAsyncScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            context.OutboxMessages.Add(new OutboxMessage
            {
                Id = messageId,
                MessageType = "ClientBookingConfirmedEmail",
                AggregateType = nameof(Booking),
                AggregateId = Guid.NewGuid(),
                IdempotencyKey = $"test:{messageId}",
                PayloadJson = "{}",
                CreatedAtUtc = fixedNow.AddHours(-2),
                NextAttemptAtUtc = fixedNow.AddHours(-1),
                Status = OutboxMessageStatus.DeadLettered,
                LastError = "Resend unavailable"
            });
            await context.SaveChangesAsync(cancellationToken);

            var service = scope.ServiceProvider.GetRequiredService<OpsMonitoringService>();
            var alerts = await service.EvaluateAlertsAsync(cancellationToken);

            Assert.Contains(
                alerts,
                alert => alert.Kind == OpsAlertKind.OutboxDeadLetter
                         && alert.Context["messageId"] == messageId.ToString());
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<Guid> SeedPendingApprovalBookingAsync(
        IServiceProvider services,
        DateTime pendingSinceUtc,
        CancellationToken cancellationToken)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

        var clientId = Guid.NewGuid();
        var user = new ApplicationUser
        {
            Id = clientId,
            UserName = "ops-pending@test.local",
            Email = "ops-pending@test.local",
            EmailConfirmed = true,
            DisplayName = "Ops Pending",
            Role = UserRole.Client
        };

        var createResult = await userManager.CreateAsync(user, "Password123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(error => error.Description)));
        }

        var slot = await context.AvailabilitySlots.AsNoTracking().FirstAsync(cancellationToken);
        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            AvailabilitySlotId = slot.Id,
            SlotStartUtc = pendingSinceUtc.AddDays(2),
            SlotEndUtc = pendingSinceUtc.AddDays(2).AddHours(1),
            DeliveryMethod = DeliveryMethod.Chat,
            Status = BookingStatus.PendingApproval,
            CreatedAt = pendingSinceUtc.AddHours(-8),
        });

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            Status = PaymentStatus.UnderReview,
            Amount = 500,
            CreatedAt = pendingSinceUtc,
            UpdatedAt = pendingSinceUtc
        });

        context.BookingStatusAudits.Add(new BookingStatusAudit
        {
            Id = Guid.NewGuid(),
            BookingId = bookingId,
            FromStatus = BookingStatus.PendingPayment,
            ToStatus = BookingStatus.PendingApproval,
            Actor = AuditActor.Client,
            ActorUserId = clientId,
            AtUtc = pendingSinceUtc
        });

        await context.SaveChangesAsync(cancellationToken);
        return bookingId;
    }

    private static IServiceProvider CreateServices(
        string connectionString,
        DateTime utcNow,
        DateTime? startedAtUtc = null)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddSingleton<IApplicationStartedAtProvider>(
            new FixedApplicationStartedAtProvider(startedAtUtc ?? utcNow.AddHours(-1)));
        services.Configure<OpsMonitoringOptions>(_ => { });
        services.Configure<BackgroundJobOptions>(options => options.Enabled = true);
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<OpsMonitoringService>();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        services.Configure<SeedOptions>(options =>
        {
            options.ConsultantWhatsAppNumber = "+201012345678";
            options.VodafoneCashNumber = "01012345678";
            options.InstaPayHandle = "test@instapay";
        });
        services.Configure<AdminSeedOptions>(options =>
        {
            options.Email = "admin@test.local";
            options.Password = "TestPass123!";
        });

        return services.BuildServiceProvider();
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class FixedApplicationStartedAtProvider(DateTime startedAtUtc) : IApplicationStartedAtProvider
    {
        public DateTime StartedAtUtc => startedAtUtc;
    }
}
