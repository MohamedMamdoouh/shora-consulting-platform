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
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class OutboxEmailRendererTests
{
    private readonly SqlServerFixture _sqlServer;

    public OutboxEmailRendererTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RenderAsync_renders_client_booking_confirmed_email_from_outbox_payload()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var services = CreateServices(connectionString);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var (bookingId, clientId, paymentId, receiptId) =
                await SeedConfirmedBookingContextAsync(services, cancellationToken);

            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
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
                CreatedAtUtc = DateTime.UtcNow,
                NextAttemptAtUtc = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending
            };

            await using var scope = services.CreateAsyncScope();
            var renderer = scope.ServiceProvider.GetRequiredService<IOutboxEmailRenderer>();
            var result = await renderer.RenderAsync(message, cancellationToken);

            Assert.True(result.IsSuccess);
            Assert.Contains("client@test.local", result.Value!.ToEmail);
            Assert.Contains("تم تأكيد حجزك", result.Value.Subject);
            Assert.Contains("تم تأكيد حجزك", result.Value.HtmlBody);
            Assert.DoesNotContain("{{", result.Value.HtmlBody);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<(Guid BookingId, Guid ClientId, Guid PaymentId, Guid ReceiptId)>
        SeedConfirmedBookingContextAsync(
            IServiceProvider services,
            CancellationToken cancellationToken)
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
            DeliveryMethod = DeliveryMethod.VoiceCall,
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

        await context.SaveChangesAsync(cancellationToken);
        return (bookingId, clientId, paymentId, receiptId);
    }

    private static IServiceProvider CreateServices(string connectionString)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IEmailTemplateService, EmailTemplateService>();
        services.AddSingleton<TransactionEmailLinks>();
        services.AddScoped<IOutboxEmailRenderer, OutboxEmailRenderer>();
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
}
