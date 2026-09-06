using System.Net;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Options;

namespace Shora.Infrastructure.Services;

public sealed class R2FileStorage : IFileStorage, IDisposable
{
    private const string TempPrefix = "temp/";

    private readonly IAmazonS3 _s3Client;
    private readonly string _bucketName;
    private readonly Protocol _protocol;

    public R2FileStorage(IOptions<StorageOptions> options)
    {
        var storageOptions = options.Value;

        if (string.IsNullOrWhiteSpace(storageOptions.Endpoint))
        {
            throw new InvalidOperationException(
                "Storage:Endpoint is not configured. Set it via user-secrets or environment variables.");
        }

        if (string.IsNullOrWhiteSpace(storageOptions.AccessKeyId))
        {
            throw new InvalidOperationException("Storage:AccessKeyId is not configured.");
        }

        if (string.IsNullOrWhiteSpace(storageOptions.SecretAccessKey))
        {
            throw new InvalidOperationException("Storage:SecretAccessKey is not configured.");
        }

        if (string.IsNullOrWhiteSpace(storageOptions.ReceiptBucket))
        {
            throw new InvalidOperationException("Storage:ReceiptBucket is not configured.");
        }

        _protocol = storageOptions.Endpoint.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
            ? Protocol.HTTPS
            : Protocol.HTTP;

        var config = new AmazonS3Config
        {
            ServiceURL = storageOptions.Endpoint,
            ForcePathStyle = true,
            AuthenticationRegion = "auto",
            UseHttp = _protocol == Protocol.HTTP
        };

        _s3Client = new AmazonS3Client(
            storageOptions.AccessKeyId,
            storageOptions.SecretAccessKey,
            config);
        _bucketName = storageOptions.ReceiptBucket;
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
        var request = CreateUploadRequest(
            _bucketName,
            tempPath,
            content,
            contentType,
            disablePayloadSigning: _protocol == Protocol.HTTPS);

        await _s3Client.PutObjectAsync(request, cancellationToken);
        return tempPath;
    }

    public async Task FinalizeAsync(
        string tempPath,
        string finalPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(tempPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(finalPath);

        if (!await ExistsAsync(tempPath, cancellationToken))
        {
            throw CreateNotFoundException($"Temporary blob '{tempPath}' was not found.");
        }

        await _s3Client.CopyObjectAsync(
            CreateCopyObjectRequest(_bucketName, tempPath, _bucketName, finalPath),
            cancellationToken);

        await _s3Client.DeleteObjectAsync(_bucketName, tempPath, cancellationToken);
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

        if (!await ExistsAsync(blobPath, cancellationToken))
        {
            throw CreateNotFoundException($"Blob '{blobPath}' was not found.");
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = _bucketName,
            Key = blobPath,
            Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(validity),
            Protocol = _protocol
        };

        return _s3Client.GetPreSignedURL(request);
    }

    public async Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        await _s3Client.DeleteObjectAsync(_bucketName, blobPath, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobPath);

        try
        {
            await _s3Client.GetObjectMetadataAsync(_bucketName, blobPath, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception ex) when (IsNotFound(ex))
        {
            return false;
        }
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

        var cutoff = DateTime.UtcNow.Subtract(maxAge);
        var deletedCount = 0;
        string? continuationToken = null;

        do
        {
            var listResponse = await _s3Client.ListObjectsV2Async(
                new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = prefix,
                    ContinuationToken = continuationToken
                },
                cancellationToken);

            foreach (var s3Object in listResponse.S3Objects ?? [])
            {
                if (s3Object.LastModified > cutoff)
                {
                    continue;
                }

                await _s3Client.DeleteObjectAsync(_bucketName, s3Object.Key, cancellationToken);
                deletedCount++;
            }

            continuationToken = listResponse.IsTruncated == true ? listResponse.NextContinuationToken : null;
        }
        while (continuationToken is not null);

        return deletedCount;
    }

    public void Dispose()
    {
        _s3Client.Dispose();
    }

    internal static PutObjectRequest CreateUploadRequest(
        string bucketName,
        string key,
        Stream content,
        string contentType,
        bool disablePayloadSigning = false)
    {
        var request = new PutObjectRequest
        {
            BucketName = bucketName,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        };

        if (disablePayloadSigning)
        {
            request.DisablePayloadSigning = true;
            request.DisableDefaultChecksumValidation = true;
        }

        return request;
    }

    internal static CopyObjectRequest CreateCopyObjectRequest(
        string sourceBucket,
        string sourceKey,
        string destinationBucket,
        string destinationKey)
    {
        return new CopyObjectRequest
        {
            SourceBucket = sourceBucket,
            SourceKey = sourceKey,
            DestinationBucket = destinationBucket,
            DestinationKey = destinationKey
        };
    }

    private static AmazonS3Exception CreateNotFoundException(string message)
    {
        return new AmazonS3Exception(message)
        {
            StatusCode = HttpStatusCode.NotFound,
            ErrorCode = "NoSuchKey"
        };
    }

    private static bool IsNotFound(AmazonS3Exception ex)
    {
        return ex.StatusCode == HttpStatusCode.NotFound
            || string.Equals(ex.ErrorCode, "NoSuchKey", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.ErrorCode, "NotFound", StringComparison.OrdinalIgnoreCase)
            || string.Equals(ex.ErrorCode, "NoSuchBucket", StringComparison.OrdinalIgnoreCase);
    }
}
