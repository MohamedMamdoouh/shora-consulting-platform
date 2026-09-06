using System.Reflection;
using System.Text;
using Amazon;
using Amazon.Runtime.Internal;
using Amazon.S3;
using Amazon.S3.Model;
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

        var request = R2FileStorage.CreateUploadRequest("receipts", "temp/abc", content, "image/png");

        Assert.True(request.DisablePayloadSigning);
        Assert.True(request.DisableDefaultChecksumValidation);
        Assert.False(request.AutoCloseStream);
        Assert.Equal("image/png", request.ContentType);
        Assert.Equal("receipts", request.BucketName);
        Assert.Equal("temp/abc", request.Key);
        Assert.Same(content, request.InputStream);
    }

    [Fact]
    public void Constructor_enables_sigv4_for_r2_presigned_urls()
    {
        AWSConfigsS3.UseSignatureVersion4 = false;

        using var storage = new R2FileStorage(Options.Create(new StorageOptions
        {
            Endpoint = "https://example.r2.cloudflarestorage.com",
            AccessKeyId = "id",
            SecretAccessKey = "secret",
            ReceiptBucket = "receipts"
        }));

        Assert.True(AWSConfigsS3.UseSignatureVersion4);
    }

    [Fact]
    public void Awssdk_copy_object_always_sends_tagging_directive_rejected_by_r2()
    {
        var marshallerType = typeof(AmazonS3Client).Assembly.GetTypes()
            .Single(type => type.Name == "CopyObjectRequestMarshaller");
        var instance = marshallerType
            .GetField("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?.GetValue(null)
            ?? marshallerType
                .GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
                ?.GetValue(null);
        Assert.NotNull(instance);

        var marshall = marshallerType
            .GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .First(method => method.Name == "Marshall" && method.GetParameters().Length == 1);
        var marshalled = marshall.Invoke(instance, [
            new CopyObjectRequest
            {
                SourceBucket = "receipts",
                SourceKey = "temp/abc",
                DestinationBucket = "receipts",
                DestinationKey = "receipts/final"
            }
        ]);

        Assert.NotNull(marshalled);
        var headers = Assert.IsAssignableFrom<IRequest>(marshalled).Headers;
        Assert.True(
            headers.TryGetValue("x-amz-tagging-directive", out var directive),
            "AWSSDK.S3 3.7 CopyObject always emits x-amz-tagging-directive; R2 returns 501 for that header.");
        Assert.Equal("COPY", directive);
    }
}
