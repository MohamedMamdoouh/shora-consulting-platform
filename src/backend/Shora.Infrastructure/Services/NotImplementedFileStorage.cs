using Shora.Application.Abstractions;

namespace Shora.Infrastructure.Services;

public sealed class NotImplementedFileStorage : IFileStorage
{
    public Task<string> UploadTempAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("File storage is implemented in spec 05.");

    public Task FinalizeAsync(string tempPath, string finalPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("File storage is implemented in spec 05.");

    public Task<string> GetReadUrlAsync(string blobPath, TimeSpan validity, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("File storage is implemented in spec 05.");

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
        => throw new NotImplementedException("File storage is implemented in spec 05.");
}
