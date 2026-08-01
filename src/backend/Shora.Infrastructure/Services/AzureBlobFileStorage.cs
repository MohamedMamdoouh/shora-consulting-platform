using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;

namespace Shora.Infrastructure.Services;

public sealed class AzureBlobFileStorage : IFileStorage
{
    private const string TempPrefix = "temp/";

    private readonly BlobServiceClient _blobServiceClient;
    private readonly string _containerName;

    public AzureBlobFileStorage(IOptions<StorageOptions> options)
    {
        var storageOptions = options.Value;

        if (string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
        {
            throw new InvalidOperationException(
                "Storage:ConnectionString is not configured. Set it via user-secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(storageOptions.ReceiptContainer))
        {
            throw new InvalidOperationException("Storage:ReceiptContainer is not configured.");
        }

        _blobServiceClient = new BlobServiceClient(storageOptions.ConnectionString);
        _containerName = storageOptions.ReceiptContainer;
    }

    public async Task<string> UploadTempAsync(
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (string.IsNullOrWhiteSpace(contentType))
        {
            throw new ArgumentException("Content type is required.", nameof(contentType));
        }

        var tempPath = $"{TempPrefix}{Guid.NewGuid():N}";
        var container = await GetContainerClientAsync(cancellationToken);
        var blobClient = container.GetBlobClient(tempPath);

        await blobClient.UploadAsync(
            content,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = contentType }
            },
            cancellationToken);

        return tempPath;
    }

    public async Task FinalizeAsync(
        string tempPath,
        string finalPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);

        var container = await GetContainerClientAsync(cancellationToken);
        var sourceClient = container.GetBlobClient(tempPath);
        var destinationClient = container.GetBlobClient(finalPath);

        if (!await sourceClient.ExistsAsync(cancellationToken))
        {
            throw new RequestFailedException(
                404,
                $"Temporary blob '{tempPath}' was not found.",
                BlobErrorCode.BlobNotFound.ToString(),
                null);
        }

        var copyOperation = await destinationClient.StartCopyFromUriAsync(
            sourceClient.Uri,
            cancellationToken: cancellationToken);

        await copyOperation.WaitForCompletionAsync(cancellationToken);
        await sourceClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public async Task<string> GetReadUrlAsync(
        string blobPath,
        TimeSpan validity,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        if (validity <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(validity), "Validity must be greater than zero.");
        }

        var container = await GetContainerClientAsync(cancellationToken);
        var blobClient = container.GetBlobClient(blobPath);

        if (!await blobClient.ExistsAsync(cancellationToken))
        {
            throw new RequestFailedException(
                404,
                $"Blob '{blobPath}' was not found.",
                BlobErrorCode.BlobNotFound.ToString(),
                null);
        }

        if (!blobClient.CanGenerateSasUri)
        {
            throw new InvalidOperationException(
                "Cannot generate a SAS URL. Ensure Storage:ConnectionString includes the account key or use a compatible credential.");
        }

        var expiresOn = DateTimeOffset.UtcNow.Add(validity);
        var sasBuilder = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn);

        return blobClient.GenerateSasUri(sasBuilder).ToString();
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        var container = await GetContainerClientAsync(cancellationToken);
        var blobClient = container.GetBlobClient(blobPath);
        await blobClient.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: cancellationToken);
    }

    public async Task<int> DeleteBlobsWithPrefixOlderThanAsync(
        string prefix,
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);

        if (maxAge <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(maxAge), "Max age must be greater than zero.");
        }

        var cutoff = DateTimeOffset.UtcNow.Subtract(maxAge);
        var container = await GetContainerClientAsync(cancellationToken);
        var deletedCount = 0;

        await foreach (var blobItem in container.GetBlobsAsync(prefix: prefix, cancellationToken: cancellationToken))
        {
            if (blobItem.Properties.LastModified is null || blobItem.Properties.LastModified > cutoff)
            {
                continue;
            }

            var blobClient = container.GetBlobClient(blobItem.Name);
            var deleted = await blobClient.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                cancellationToken: cancellationToken);

            if (deleted)
            {
                deletedCount++;
            }
        }

        return deletedCount;
    }

    private async Task<BlobContainerClient> GetContainerClientAsync(CancellationToken cancellationToken)
    {
        var containerClient = _blobServiceClient.GetBlobContainerClient(_containerName);
        await containerClient.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: cancellationToken);
        return containerClient;
    }
}
