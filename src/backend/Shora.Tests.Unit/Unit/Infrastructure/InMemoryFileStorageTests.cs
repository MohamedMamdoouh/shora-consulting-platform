using System.Text;
using Shora.Tests.Common;

namespace Shora.Tests.Unit.Infrastructure;

public sealed class InMemoryFileStorageTests
{
    [Fact]
    public async Task UploadTempAsync_stores_blob_under_temp_prefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("receipt-bytes"));

        var tempPath = await fileStorage.UploadTempAsync(content, "image/png", cancellationToken);

        Assert.StartsWith("temp/", tempPath, StringComparison.Ordinal);
        Assert.True(fileStorage.TryGetBlob(tempPath, out _));
    }

    [Fact]
    public async Task Full_lifecycle_upload_finalize_read_and_delete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        const string payload = "receipt-image-bytes";
        var finalPath = $"receipts/{Guid.NewGuid():N}.png";

        string tempPath;
        await using (var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(payload)))
        {
            tempPath = await fileStorage.UploadTempAsync(uploadStream, "image/png", cancellationToken);
            await fileStorage.FinalizeAsync(tempPath, finalPath, cancellationToken);
        }

        Assert.False(fileStorage.TryGetBlob(tempPath, out _));
        Assert.True(fileStorage.TryGetBlob(finalPath, out var bytes));
        Assert.Equal(Encoding.UTF8.GetBytes(payload), bytes);

        var readUrl = await fileStorage.GetReadUrlAsync(finalPath, TimeSpan.FromMinutes(5), cancellationToken);
        Assert.Equal($"memory://{finalPath}", readUrl);

        await fileStorage.DeleteAsync(finalPath, cancellationToken);

        var getAfterDelete = () =>
            fileStorage.GetReadUrlAsync(finalPath, TimeSpan.FromMinutes(5), cancellationToken);

        await Assert.ThrowsAsync<FileNotFoundException>(getAfterDelete);
    }

    [Fact]
    public async Task FinalizeAsync_throws_when_temp_blob_is_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();

        var act = () => fileStorage.FinalizeAsync(
            $"temp/{Guid.NewGuid():N}",
            $"receipts/{Guid.NewGuid():N}.png",
            cancellationToken);

        await Assert.ThrowsAsync<FileNotFoundException>(act);
    }

    [Fact]
    public async Task ExistsAsync_reflects_upload_finalize_and_delete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("x"));

        var tempPath = await fileStorage.UploadTempAsync(content, "image/png", cancellationToken);
        Assert.True(await fileStorage.ExistsAsync(tempPath, cancellationToken));

        const string finalPath = "receipts/final.png";
        await fileStorage.FinalizeAsync(tempPath, finalPath, cancellationToken);
        Assert.False(await fileStorage.ExistsAsync(tempPath, cancellationToken));
        Assert.True(await fileStorage.ExistsAsync(finalPath, cancellationToken));

        await fileStorage.DeleteAsync(finalPath, cancellationToken);
        Assert.False(await fileStorage.ExistsAsync(finalPath, cancellationToken));
    }

    [Fact]
    public async Task DeleteBlobsWithPrefixOlderThanAsync_removes_only_stale_temp_objects()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = new InMemoryFileStorage();
        fileStorage.AddBlob("temp/stale", Encoding.UTF8.GetBytes("old"), DateTime.UtcNow.AddHours(-2));
        fileStorage.AddBlob("temp/fresh", Encoding.UTF8.GetBytes("new"), DateTime.UtcNow);
        fileStorage.AddBlob("receipts/keep", Encoding.UTF8.GetBytes("keep"), DateTime.UtcNow.AddHours(-2));

        var deleted = await fileStorage.DeleteBlobsWithPrefixOlderThanAsync(
            "temp/",
            TimeSpan.FromHours(1),
            cancellationToken);

        Assert.Equal(1, deleted);
        Assert.False(await fileStorage.ExistsAsync("temp/stale", cancellationToken));
        Assert.True(await fileStorage.ExistsAsync("temp/fresh", cancellationToken));
        Assert.True(await fileStorage.ExistsAsync("receipts/keep", cancellationToken));
    }
}
