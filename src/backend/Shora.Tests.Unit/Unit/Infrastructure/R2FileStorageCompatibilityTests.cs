using System.Text;
using Microsoft.Extensions.Options;
using Shora.Application.Options;
using Shora.Infrastructure.Services;

namespace Shora.Tests.Unit.Infrastructure;

public sealed class R2FileStorageCompatibilityTests
{
    [Fact]
    public void CreateUploadRequest_disables_streaming_sigv4_required_by_r2()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("receipt-bytes"));

        var request = R2FileStorage.CreateUploadRequest(
            "shora-receipts",
            "temp/abc",
            content,
            "image/png",
            disablePayloadSigning: true);

        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.False(request.AutoCloseStream);
        Assert.Equal("image/png", request.ContentType);
        Assert.Equal("shora-receipts", request.BucketName);
        Assert.Equal("temp/abc", request.Key);
        Assert.Same(content, request.InputStream);
    }

    [Fact]
    public void CreateCopyObjectRequest_omits_tagging_directive_required_by_r2()
    {
        var request = R2FileStorage.CreateCopyObjectRequest(
            "shora-receipts",
            "temp/abc",
            "shora-receipts",
            "receipts/xyz.png");

        Assert.Null(request.TaggingDirective);
        Assert.Equal("shora-receipts", request.SourceBucket);
        Assert.Equal("temp/abc", request.SourceKey);
        Assert.Equal("shora-receipts", request.DestinationBucket);
        Assert.Equal("receipts/xyz.png", request.DestinationKey);
    }

    [Fact]
    public void Constructor_accepts_r2_endpoint_configuration()
    {
        using var storage = new R2FileStorage(Options.Create(new StorageOptions
        {
            Endpoint = "https://example.r2.cloudflarestorage.com",
            AccessKeyId = "id",
            SecretAccessKey = "secret",
            ReceiptBucket = "shora-receipts"
        }));
    }
}
