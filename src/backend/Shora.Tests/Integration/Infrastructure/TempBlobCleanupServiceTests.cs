using Microsoft.Extensions.DependencyInjection;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Application.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

public class TempBlobCleanupServiceTests
{
    [Fact]
    public async Task RunAsync_deletes_temp_blob_older_than_max_age()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        var oldTempPath = "temp/oldblob";
        fileStorage.AddBlob(oldTempPath, [1, 2, 3], DateTime.UtcNow.AddHours(-25));

        var services = CreateServices(fileStorage);
        await using var scope = services.CreateAsyncScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<TempBlobCleanupService>();

        var deletedCount = await cleanupService.RunAsync(cancellationToken);

        Assert.Equal(1, deletedCount);
        Assert.False(fileStorage.TryGetBlob(oldTempPath, out _));
    }

    [Fact]
    public async Task RunAsync_keeps_recent_temp_blob()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        var recentTempPath = "temp/recentblob";
        fileStorage.AddBlob(recentTempPath, [1, 2, 3], DateTime.UtcNow.AddHours(-1));

        var services = CreateServices(fileStorage);
        await using var scope = services.CreateAsyncScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<TempBlobCleanupService>();

        var deletedCount = await cleanupService.RunAsync(cancellationToken);

        Assert.Equal(0, deletedCount);
        Assert.True(fileStorage.TryGetBlob(recentTempPath, out _));
    }

    [Fact]
    public async Task RunAsync_does_not_delete_finalized_receipt_blobs()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        var receiptPath = "receipts/payment/receipt";
        fileStorage.AddBlob(receiptPath, [1, 2, 3], DateTime.UtcNow.AddDays(-30));

        var services = CreateServices(fileStorage);
        await using var scope = services.CreateAsyncScope();
        var cleanupService = scope.ServiceProvider.GetRequiredService<TempBlobCleanupService>();

        var deletedCount = await cleanupService.RunAsync(cancellationToken);

        Assert.Equal(0, deletedCount);
        Assert.True(fileStorage.TryGetBlob(receiptPath, out _));
    }

    [Fact]
    public async Task DeleteBlobsWithPrefixOlderThanAsync_filters_by_prefix_and_age()
    {
        var fileStorage = new InMemoryFileStorage();
        fileStorage.AddBlob("temp/old", [1], DateTime.UtcNow.AddHours(-25));
        fileStorage.AddBlob("temp/recent", [2], DateTime.UtcNow.AddHours(-1));
        fileStorage.AddBlob("receipts/old", [3], DateTime.UtcNow.AddDays(-30));

        var deletedCount = await fileStorage.DeleteBlobsWithPrefixOlderThanAsync(
            "temp/",
            TimeSpan.FromHours(24),
            TestContext.Current.CancellationToken);

        Assert.Equal(1, deletedCount);
        Assert.False(fileStorage.TryGetBlob("temp/old", out _));
        Assert.True(fileStorage.TryGetBlob("temp/recent", out _));
        Assert.True(fileStorage.TryGetBlob("receipts/old", out _));
    }

    private static IServiceProvider CreateServices(InMemoryFileStorage fileStorage)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<IFileStorage>(fileStorage);
        services.Configure<BackgroundJobOptions>(options =>
        {
            options.TempBlobMaxAgeHours = 24;
        });
        services.AddScoped<TempBlobCleanupService>();
        return services.BuildServiceProvider();
    }
}
