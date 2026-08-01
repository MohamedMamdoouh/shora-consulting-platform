using Shora.Application.Abstractions;

namespace Shora.Infrastructure.Services;

public sealed class NotImplementedFileStorage : IFileStorage
{
    public Task<string> UploadTempAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Configure Storage:ConnectionString to enable Azure Blob receipt storage.");

    public Task FinalizeAsync(string tempPath, string finalPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Configure Storage:ConnectionString to enable Azure Blob receipt storage.");

    public Task<string> GetReadUrlAsync(string blobPath, TimeSpan validity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Configure Storage:ConnectionString to enable Azure Blob receipt storage.");

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Configure Storage:ConnectionString to enable Azure Blob receipt storage.");

    public Task<int> DeleteBlobsWithPrefixOlderThanAsync(
        string prefix,
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
        => throw new NotImplementedException("Configure Storage:ConnectionString to enable Azure Blob receipt storage.");
}
