using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Azurite")]
public sealed class AzureBlobFileStorageTests
{
    private readonly AzuriteFixture _azurite;

    public AzureBlobFileStorageTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    [Fact]
    public async Task UploadTempAsync_stores_blob_under_temp_prefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = CreateFileStorage();
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("receipt-bytes"));

        var tempPath = await fileStorage.UploadTempAsync(content, "image/png", cancellationToken);

        Assert.StartsWith("temp/", tempPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Full_lifecycle_upload_finalize_read_and_delete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = CreateFileStorage();
        const string payload = "receipt-image-bytes";
        var finalPath = $"receipts/{Guid.NewGuid():N}.png";

        await using (var uploadStream = new MemoryStream(Encoding.UTF8.GetBytes(payload)))
        {
            var tempPath = await fileStorage.UploadTempAsync(uploadStream, "image/png", cancellationToken);
            await fileStorage.FinalizeAsync(tempPath, finalPath, cancellationToken);
        }

        var readUrl = await fileStorage.GetReadUrlAsync(finalPath, TimeSpan.FromMinutes(5), cancellationToken);

        using var httpClient = new HttpClient();
        var downloaded = await httpClient.GetByteArrayAsync(readUrl, cancellationToken);
        Assert.Equal(Encoding.UTF8.GetBytes(payload), downloaded);

        await fileStorage.DeleteAsync(finalPath, cancellationToken);

        var getAfterDelete = async () =>
            await fileStorage.GetReadUrlAsync(finalPath, TimeSpan.FromMinutes(5), cancellationToken);

        var exception = await Assert.ThrowsAsync<Azure.RequestFailedException>(getAfterDelete);
        Assert.Equal((int)HttpStatusCode.NotFound, exception.Status);
    }

    [Fact]
    public async Task FinalizeAsync_throws_when_temp_blob_is_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = CreateFileStorage();

        var act = () => fileStorage.FinalizeAsync(
            $"temp/{Guid.NewGuid():N}",
            $"receipts/{Guid.NewGuid():N}.png",
            cancellationToken);

        var exception = await Assert.ThrowsAsync<Azure.RequestFailedException>(act);
        Assert.Equal((int)HttpStatusCode.NotFound, exception.Status);
    }

    private AzureBlobFileStorage CreateFileStorage()
    {
        var services = new ServiceCollection();
        services.AddOptions<StorageOptions>().Configure(options =>
        {
            options.ConnectionString = _azurite.BlobConnectionString;
            options.ReceiptContainer = $"receipts-{Guid.NewGuid():N}";
        });

        var serviceProvider = services.BuildServiceProvider();
        return new AzureBlobFileStorage(serviceProvider.GetRequiredService<IOptions<StorageOptions>>());
    }
}
