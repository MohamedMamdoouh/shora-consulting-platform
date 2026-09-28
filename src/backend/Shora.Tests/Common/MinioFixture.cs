using Testcontainers.Minio;

namespace Shora.Tests.Common;

public sealed class MinioFixture : IAsyncLifetime
{
    // Docker Hub removed the minio/minio repository. The same release is still published on Quay.
    private readonly MinioContainer _container = new MinioBuilder("quay.io/minio/minio:RELEASE.2024-12-18T13-15-44Z")
        .Build();

    public string Endpoint =>
        new UriBuilder(Uri.UriSchemeHttp, _container.Hostname, _container.GetMappedPublicPort(MinioBuilder.MinioPort))
            .Uri
            .GetLeftPart(UriPartial.Authority);

    public string AccessKeyId => MinioBuilder.DefaultUsername;

    public string SecretAccessKey => MinioBuilder.DefaultPassword;

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Minio")]
public sealed class MinioCollection : ICollectionFixture<MinioFixture>;
