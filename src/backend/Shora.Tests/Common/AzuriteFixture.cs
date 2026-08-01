using Testcontainers.Azurite;

namespace Shora.Tests.Common;

public sealed class AzuriteFixture : IAsyncLifetime
{
    private readonly AzuriteContainer _container = new AzuriteBuilder("mcr.microsoft.com/azure-storage/azurite:3.35.0")
        .WithCommand("--skipApiVersionCheck")
        .Build();

    public string BlobConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

[CollectionDefinition("Azurite")]
public sealed class AzuriteCollection : ICollectionFixture<AzuriteFixture>;
