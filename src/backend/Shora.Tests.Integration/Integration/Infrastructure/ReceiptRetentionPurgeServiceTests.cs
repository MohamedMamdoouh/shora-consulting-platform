using Microsoft.AspNetCore.Identity;
using Npgsql;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Postgres")]
public class ReceiptRetentionPurgeServiceTests
{
    private readonly PostgresFixture _sqlServer;

    public ReceiptRetentionPurgeServiceTests(PostgresFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_purges_finalized_receipt_older_than_retention()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var fileStorage = new InMemoryFileStorage();
        var services = CreateServices(connectionString, fileStorage);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var receiptId = await SeedReceiptAsync(
                services,
                fileStorage,
                uploadedAtUtc: DateTime.UtcNow.AddMonths(-13),
                blobState: BlobState.Finalized,
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var purgeService = scope.ServiceProvider.GetRequiredService<ReceiptRetentionPurgeService>();
            var processedCount = await purgeService.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await context.PaymentReceipts.AsNoTracking().SingleAsync(r => r.Id == receiptId, cancellationToken);

            Assert.Equal(BlobState.Missing, receipt.BlobState);
            Assert.Equal("[purged]", receipt.OriginalFileName);
            Assert.Equal(string.Empty, receipt.ContentType);
            Assert.Equal(string.Empty, receipt.ContentHashSha256);
            Assert.Equal(0, receipt.SizeBytes);
            Assert.Null(receipt.SenderReference);
            Assert.False(fileStorage.TryGetBlob(receipt.BlobPath, out _));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_skips_recent_receipt()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new NpgsqlConnectionStringBuilder(connectionString).Database!;
        var fileStorage = new InMemoryFileStorage();
        var services = CreateServices(connectionString, fileStorage);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);

            var receiptId = await SeedReceiptAsync(
                services,
                fileStorage,
                uploadedAtUtc: DateTime.UtcNow.AddDays(-30),
                blobState: BlobState.Finalized,
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var purgeService = scope.ServiceProvider.GetRequiredService<ReceiptRetentionPurgeService>();
            var processedCount = await purgeService.RunAsync(cancellationToken);

            Assert.Equal(0, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await context.PaymentReceipts.AsNoTracking().SingleAsync(r => r.Id == receiptId, cancellationToken);

            Assert.Equal(BlobState.Finalized, receipt.BlobState);
            Assert.True(fileStorage.TryGetBlob(receipt.BlobPath, out _));
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
        var fileStorage = new InMemoryFileStorage();
        var services = CreateServices(connectionString, fileStorage);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);
            await DatabaseSeeder.SeedAsync(services, cancellationToken);
            await SeedReceiptAsync(
                services,
                fileStorage,
                uploadedAtUtc: DateTime.UtcNow.AddMonths(-13),
                blobState: BlobState.Finalized,
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var purgeService = scope.ServiceProvider.GetRequiredService<ReceiptRetentionPurgeService>();

            Assert.Equal(1, await purgeService.RunAsync(cancellationToken));
            Assert.Equal(0, await purgeService.RunAsync(cancellationToken));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static IServiceProvider CreateServices(string connectionString, InMemoryFileStorage fileStorage)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IDateTimeProvider, SystemDateTimeProvider>();
        services.AddSingleton<IFileStorage>(fileStorage);
        services.AddScoped<SlotGenerationService>();
        services.AddScoped<ReceiptRetentionPurgeService>();

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

    private static async Task<Guid> SeedReceiptAsync(
        IServiceProvider services,
        InMemoryFileStorage fileStorage,
        DateTime uploadedAtUtc,
        BlobState blobState,
        CancellationToken cancellationToken)
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
            Status = BookingStatus.PendingApproval,
            CreatedAt = now.AddHours(-2)
        });

        var paymentId = Guid.NewGuid();
        context.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            Status = PaymentStatus.UnderReview,
            Amount = 500m,
            Currency = "EGP",
            CreatedAt = now.AddHours(-2),
            UpdatedAt = now.AddHours(-2)
        });

        var receiptId = Guid.NewGuid();
        var blobPath = $"receipts/{paymentId}/{receiptId}";
        fileStorage.AddBlob(blobPath, [0x89, 0x50, 0x4E, 0x47], uploadedAtUtc);

        context.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = receiptId,
            PaymentId = paymentId,
            BlobPath = blobPath,
            OriginalFileName = "receipt.png",
            ContentType = "image/png",
            ContentHashSha256 = "abc123",
            SizeBytes = 4,
            SenderReference = "wallet-ref",
            UploadedAtUtc = uploadedAtUtc,
            BlobState = blobState,
            MalwareScanStatus = MalwareScanStatus.Pending,
            ReviewStatus = ReceiptReviewStatus.Pending
        });

        await context.SaveChangesAsync(cancellationToken);
        return receiptId;
    }
}
