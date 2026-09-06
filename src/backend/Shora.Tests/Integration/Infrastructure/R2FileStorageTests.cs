using System.Net;
using System.Text;
using Amazon.S3;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;
using Shora.Infrastructure.Services;
using Shora.Tests.Common;

namespace Shora.Tests.Integration.Infrastructure;

[Collection("Minio")]
public sealed class R2FileStorageTests
{
    private readonly MinioFixture _minio;

    public R2FileStorageTests(MinioFixture minio)
    {
        _minio = minio;
    }

    [Fact]
    public async Task UploadTempAsync_stores_blob_under_temp_prefix()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = await CreateFileStorageAsync(cancellationToken);
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes("receipt-bytes"));

        var tempPath = await fileStorage.UploadTempAsync(content, "image/png", cancellationToken);

        Assert.StartsWith("temp/", tempPath, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Full_lifecycle_upload_finalize_read_and_delete()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = await CreateFileStorageAsync(cancellationToken);
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

        var exception = await Assert.ThrowsAsync<AmazonS3Exception>(getAfterDelete);
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    [Fact]
    public async Task FinalizeAsync_throws_when_temp_blob_is_missing()
    {
        var cancellationToken = TestContext.Current.CancellationToken;
        var fileStorage = await CreateFileStorageAsync(cancellationToken);

        var act = () => fileStorage.FinalizeAsync(
            $"temp/{Guid.NewGuid():N}",
            $"receipts/{Guid.NewGuid():N}.png",
            cancellationToken);

        var exception = await Assert.ThrowsAsync<AmazonS3Exception>(act);
        Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
    }

    private async Task<R2FileStorage> CreateFileStorageAsync(CancellationToken cancellationToken)
    {
        var bucketName = $"shora-receipts-{Guid.NewGuid():N}";
        var services = new ServiceCollection();
        services.AddOptions<StorageOptions>().Configure(options =>
        {
            options.Endpoint = _minio.Endpoint;
            options.AccessKeyId = _minio.AccessKeyId;
            options.SecretAccessKey = _minio.SecretAccessKey;
            options.ReceiptBucket = bucketName;
        });

        var serviceProvider = services.BuildServiceProvider();
        using var adminClient = new AmazonS3Client(
            _minio.AccessKeyId,
            _minio.SecretAccessKey,
            new AmazonS3Config
            {
                ServiceURL = _minio.Endpoint,
                ForcePathStyle = true,
                UseHttp = true
            });
        await adminClient.PutBucketAsync(bucketName, cancellationToken);

        return new R2FileStorage(serviceProvider.GetRequiredService<IOptions<StorageOptions>>());
    }
}
