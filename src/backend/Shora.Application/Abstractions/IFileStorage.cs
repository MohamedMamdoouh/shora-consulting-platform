namespace Shora.Application.Abstractions;

public interface IFileStorage
{
    Task<string> UploadTempAsync(Stream content, string contentType, CancellationToken cancellationToken = default);

    Task FinalizeAsync(string tempPath, string finalPath, CancellationToken cancellationToken = default);

    Task<string> GetReadUrlAsync(string blobPath, TimeSpan validity, CancellationToken cancellationToken = default);

    Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default);
}
