using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Domain.Entities;
using Shora.Domain.Enums;
using Shora.Infrastructure.Data;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("SqlServer")]
public class ReceiptBlobReconciliationServiceTests
{
    private readonly SqlServerFixture _sqlServer;

    public ReceiptBlobReconciliationServiceTests(SqlServerFixture sqlServer)
    {
        _sqlServer = sqlServer;
    }

    [Fact]
    public async Task RunAsync_finalizes_receipt_when_final_blob_exists()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fileStorage = new InMemoryFileStorage();
        var services = CreateServices(connectionString, fileStorage);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            var receiptId = await SeedFinalizePendingReceiptAsync(
                services,
                fileStorage,
                seedFinalBlob: true,
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ReceiptBlobReconciliationService>();
            var processedCount = await service.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await context.PaymentReceipts.AsNoTracking()
                .SingleAsync(r => r.Id == receiptId, cancellationToken);

            Assert.Equal(BlobState.Finalized, receipt.BlobState);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_marks_receipt_missing_when_final_blob_does_not_exist()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fileStorage = new InMemoryFileStorage();
        var services = CreateServices(connectionString, fileStorage);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            var receiptId = await SeedFinalizePendingReceiptAsync(
                services,
                fileStorage,
                seedFinalBlob: false,
                cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ReceiptBlobReconciliationService>();
            var processedCount = await service.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);

            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var receipt = await context.PaymentReceipts.AsNoTracking()
                .SingleAsync(r => r.Id == receiptId, cancellationToken);

            Assert.Equal(BlobState.Missing, receipt.BlobState);
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    [Fact]
    public async Task RunAsync_deletes_old_orphan_temp_blobs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var connectionString = await _sqlServer.CreateDatabaseAsync();
        var databaseName = new SqlConnectionStringBuilder(connectionString).InitialCatalog!;
        var fileStorage = new InMemoryFileStorage();
        fileStorage.AddBlob("temp/orphan", [1, 2, 3], DateTime.UtcNow.AddHours(-2));
        var services = CreateServices(connectionString, fileStorage);

        try
        {
            await TestDatabaseInitializer.MigrateAsync(services, cancellationToken);

            await using var scope = services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<ReceiptBlobReconciliationService>();
            var processedCount = await service.RunAsync(cancellationToken);

            Assert.Equal(1, processedCount);
            Assert.False(fileStorage.TryGetBlob("temp/orphan", out _));
        }
        finally
        {
            await _sqlServer.DropDatabaseAsync(databaseName);
        }
    }

    private static async Task<Guid> SeedFinalizePendingReceiptAsync(
        IServiceProvider services,
        InMemoryFileStorage fileStorage,
        bool seedFinalBlob,
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
            UserName = $"client-{clientId:N}@test.local",
            Email = $"client-{clientId:N}@test.local",
            EmailConfirmed = true,
            DisplayName = "Test Client",
            Role = UserRole.Client
        };

        var createResult = await userManager.CreateAsync(client, "Password123!");
        if (!createResult.Succeeded)
        {
            throw new InvalidOperationException(string.Join(", ", createResult.Errors.Select(e => e.Description)));
        }

        var paymentId = Guid.NewGuid();
        var bookingId = Guid.NewGuid();
        var receiptId = Guid.NewGuid();
        var blobPath = $"receipts/{paymentId}/{receiptId}";

        if (seedFinalBlob)
        {
            fileStorage.AddBlob(blobPath, [1, 2, 3], now);
        }

        context.Bookings.Add(new Booking
        {
            Id = bookingId,
            ClientId = clientId,
            SlotStartUtc = now.AddDays(1),
            SlotEndUtc = now.AddDays(1).AddHours(1),
            DeliveryMethod = DeliveryMethod.Chat,
            Status = BookingStatus.PendingApproval,
            CreatedAt = now
        });

        context.Payments.Add(new Payment
        {
            Id = paymentId,
            BookingId = bookingId,
            Amount = 500m,
            Currency = "EGP",
            Status = PaymentStatus.UnderReview,
            CreatedAt = now,
            UpdatedAt = now
        });

        context.PaymentReceipts.Add(new PaymentReceipt
        {
            Id = receiptId,
            PaymentId = paymentId,
            BlobPath = blobPath,
            BlobState = BlobState.BlobFinalizePending,
            ContentType = "image/jpeg",
            OriginalFileName = "receipt.jpg",
            ContentHashSha256 = new string('a', 64),
            UploadedAtUtc = now,
            ReviewStatus = ReceiptReviewStatus.Pending,
            MalwareScanStatus = MalwareScanStatus.Pending
        });

        await context.SaveChangesAsync(cancellationToken);
        return receiptId;
    }

    private static IServiceProvider CreateServices(string connectionString, InMemoryFileStorage fileStorage)
    {
        var services = new ServiceCollection();

        services.AddLogging();
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(connectionString));
        services.AddScoped<IApplicationDbContext>(provider => provider.GetRequiredService<ApplicationDbContext>());
        services.AddSingleton<IFileStorage>(fileStorage);
        services.Configure<BackgroundJobOptions>(options =>
        {
            options.ReconciliationTempBlobMaxAgeHours = 1;
        });
        services.AddScoped<ReceiptBlobReconciliationService>();

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>()
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        return services.BuildServiceProvider();
    }
}
