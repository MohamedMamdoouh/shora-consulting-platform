using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Common;
using Shora.Application.Email;
using Shora.Application.Email.Outbox;
using Shora.Application.Options;
using Shora.Application.Outbox;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class OutboxDispatcherServiceTests
{
    private readonly SqlServerFixture _sqlServer;

    public OutboxDispatcherServiceTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_marks_pending_message_processed_after_successful_send()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var emailSender = new RecordingEmailSender();
        var services = CreateServices(connectionString, fixedNow, emailSender);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var messageId = await SeedPendingOutboxMessageAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var processedCount = await dispatcher.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);
            Assert.Equal(1, emailSender.SendCount);

            var message = await context.OutboxMessages.AsNoTracking().SingleAsync(
                m => m.Id == messageId,
                cancellationToken);

            Assert.Equal(OutboxMessageStatus.Processed, message.Status);
            Assert.Equal(fixedNow, message.ProcessedAtUtc);
            Assert.Null(message.LastError);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_schedules_retry_after_send_failure()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var emailSender = new FailingEmailSender(failuresBeforeSuccess: int.MaxValue);
        var services = CreateServices(connectionString, fixedNow, emailSender);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var messageId = await SeedPendingOutboxMessageAsync(services, fixedNow, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var processedCount = await dispatcher.RunAsync(cancellationToken);

            Assert.Equal(0, processedCount);

            var message = await context.OutboxMessages.AsNoTracking().SingleAsync(
                m => m.Id == messageId,
                cancellationToken);

            Assert.Equal(OutboxMessageStatus.Pending, message.Status);
            Assert.Equal(1, message.AttemptCount);
            Assert.Equal(
                fixedNow + OutboxRetryPolicy.GetDelayAfterFailure(1),
                message.NextAttemptAtUtc);
            Assert.Contains("Simulated send failure", message.LastError);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_dead_letters_message_after_max_attempts()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var emailSender = new FailingEmailSender(failuresBeforeSuccess: int.MaxValue);
        var services = CreateServices(connectionString, fixedNow, emailSender);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var messageId = await SeedPendingOutboxMessageAsync(
                services,
                fixedNow,
                cancellationToken,
                attemptCount: OutboxRetryPolicy.MaxAttempts - 1);

            await using var scope = services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherService>();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

            var processedCount = await dispatcher.RunAsync(cancellationToken);

            Assert.Equal(0, processedCount);

            var message = await context.OutboxMessages.AsNoTracking().SingleAsync(
                m => m.Id == messageId,
                cancellationToken);

            Assert.Equal(OutboxMessageStatus.DeadLettered, message.Status);
            Assert.Equal(OutboxRetryPolicy.MaxAttempts, message.AttemptCount);
            Assert.Null(message.ProcessedAtUtc);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_skips_messages_not_yet_due()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fixedNow = new DateTime(2026, 8, 6, 12, 0, 0, DateTimeKind.Utc);
        var emailSender = new RecordingEmailSender();
        var services = CreateServices(connectionString, fixedNow, emailSender);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            await SeedPendingOutboxMessageAsync(
                services,
                fixedNow.AddHours(1),
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var dispatcher = scope.ServiceProvider.GetRequiredService<OutboxDispatcherService>();

            var processedCount = await dispatcher.RunAsync(cancellationToken);

            Assert.Equal(0, processedCount);
            Assert.Equal(0, emailSender.SendCount);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<Guid> SeedPendingOutboxMessageAsync(
        IServiceProvider services,
        DateTime nextAttemptAtUtc,
        CancellationToken cancellationToken,
        int attemptCount = 0)
    {
        await using var scope = services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var now = DateTime.UtcNow;

        var clientId = Guid.NewGuid();
        var client = new ApplicationUser
        {
            Id = clientId,
            UserName = "client@test.local",
            Email = "client@test.local",
            EmailConfirmed = true,
            DisplayName = "Test Client",
            Role = UserRole.Client
        };

        var createResult = await userManager.CreateAsync(client, "Password123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var bookingId = Guid.NewGuid();
        var paymentId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var slotStart = now.AddDays(3);
        var slotEnd = slotStart.AddHours(1);

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            SlotStartUtc = slotStart,
            SlotEndUtc = slotEnd,
            DeliveryMethod = DeliveryMethod.Chat,
            ContactPhone = "+201012345678",
            Status = BookingStatus.Confirmed,
            CreatedAt = now
        });

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            Amount = 500m,
            Currency = "EGP",
            Status = PaymentStatus.Approved,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = receiptId,
            PaymentId = paymentId,
            BlobPath = "receipts/test.jpg",
            BlobState = BlobState.Finalized,
            ContentType = "image/jpeg",
            OriginalFileName = "receipt.jpg",
            ContentHashSha256 = new string('a', 64),
            UploadedAtUtc = now,
            ReviewStatus = ReceiptReviewStatus.Approved,
            MalwareScanStatus = MalwareScanStatus.Clean
        });

        var messageId = Guid.NewGuid();
        context.OutboxMessages.Add(new OutboxMessage
        {
            Id = messageId,
            MessageType = OutboxMessageTypes.ClientBookingConfirmedEmail,
            AggregateType = nameof(Booking),
            AggregateId = bookingId,
            IdempotencyKey = $"{bookingId}:{OutboxMessageTypes.ClientBookingConfirmedEmail}",
            PayloadJson = JsonSerializer.Serialize(new
            {
                bookingId,
                paymentId,
                receiptId,
                clientId
            }),
            CreatedAtUtc = now,
            NextAttemptAtUtc = nextAttemptAtUtc,
            AttemptCount = attemptCount,
            Status = OutboxMessageStatus.Pending
        });

        await context.SaveChangesAsync(cancellationToken);
        return messageId;
    }

    private static IServiceProvider CreateServices(
        string connectionString,
        DateTime utcNow,
        IEmailSender emailSender)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider>(new FixedDateTimeProvider(utcNow));
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
        services.AddSingleton<TransactionEmailLinks>();
        services.AddScoped<IOutboxEmailRenderer, OutboxEmailRenderer>();
        services.AddScoped<OutboxDispatcherService>();
        services.AddSingleton(emailSender);
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
                ["Seed:InstaPayHandle"] = "test@instapay",
                ["Frontend:BaseUrl"] = "https://app.test",
                ["Brand:BrandName"] = "منصة شورى"
            })
            .Build();

        services.AddSingleton<IConfiguration>(configuration);
        services.Configure<SeedOptions>(configuration.GetSection(SeedOptions.SectionName));
        services.Configure<AdminSeedOptions>(configuration.GetSection(AdminSeedOptions.SectionName));
        services.Configure<FrontendOptions>(configuration.GetSection(FrontendOptions.SectionName));
        services.Configure<EmailBrandOptions>(configuration.GetSection(EmailBrandOptions.SectionName));

        return services.BuildServiceProvider();
    }

    private sealed class FixedDateTimeProvider(DateTime utcNow) : IDateTimeProvider
    {
        public DateTime UtcNow => utcNow;
    }

    private sealed class RecordingEmailSender : IEmailSender
    {
        public int SendCount { get; private set; }

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            SendCount++;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingEmailSender(int failuresBeforeSuccess) : IEmailSender
    {
        private int _attempts;

        public Task SendAsync(
            string toEmail,
            string subject,
            string htmlBody,
            CancellationToken cancellationToken = default)
        {
            _attempts++;

            if (_attempts <= failuresBeforeSuccess)
            {
                throw new InvalidOperationException("Simulated send failure");
            }

            return Task.CompletedTask;
        }
    }
}
