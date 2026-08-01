using System.Collections.Concurrent;
using Shora.Application.Abstractions;

namespace Shora.Tests.Common;

public sealed class InMemoryFileStorage : IFileStorage
{
    private readonly ConcurrentDictionary<string, StoredBlob> _blobs = new(StringComparer.Ordinal);

    public Task<string> UploadTempAsync(Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        _ = contentType;
        var tempPath = $"temp/{Guid.NewGuid():N}";
        _blobs[tempPath] = new StoredBlob(ReadAllBytes(content), DateTime.UtcNow);
        return Task.FromResult(tempPath);
    }

    public Task FinalizeAsync(string tempPath, string finalPath, CancellationToken cancellationToken = default)
    {
        if (!_blobs.TryRemove(tempPath, out var storedBlob))
        {
            throw new FileNotFoundException($"Temporary blob '{tempPath}' was not found.");
        }

        _blobs[finalPath] = storedBlob;
        return Task.CompletedTask;
    }

    public Task<string> GetReadUrlAsync(string blobPath, TimeSpan validity, CancellationToken cancellationToken = default)
    {
        _ = validity;

        if (!_blobs.ContainsKey(blobPath))
        {
            throw new FileNotFoundException($"Blob '{blobPath}' was not found.");
        }

        return Task.FromResult($"memory://{blobPath}");
    }

    public Task DeleteAsync(string blobPath, CancellationToken cancellationToken = default)
    {
        _blobs.TryRemove(blobPath, out _);
        return Task.CompletedTask;
    }

    public Task<int> DeleteBlobsWithPrefixOlderThanAsync(
        string prefix,
        TimeSpan maxAge,
        CancellationToken cancellationToken = default)
    {
        var cutoff = DateTime.UtcNow.Subtract(maxAge);
        var deletedCount = 0;

        foreach (var blobPath in _blobs.Keys.ToList())
        {
            if (!blobPath.StartsWith(prefix, StringComparison.Ordinal))
            {
                continue;
            }

            if (_blobs[blobPath].UploadedAtUtc > cutoff)
            {
                continue;
            }

            if (_blobs.TryRemove(blobPath, out _))
            {
                deletedCount++;
            }
        }

        return Task.FromResult(deletedCount);
    }

    public bool TryGetBlob(string blobPath, out byte[] bytes)
    {
        if (_blobs.TryGetValue(blobPath, out var storedBlob))
        {
            bytes = storedBlob.Content;
            return true;
        }

        bytes = [];
        return false;
    }

    public void SetUploadedAtUtc(string blobPath, DateTime uploadedAtUtc)
    {
        if (_blobs.TryGetValue(blobPath, out var storedBlob))
        {
            _blobs[blobPath] = storedBlob with { UploadedAtUtc = uploadedAtUtc };
        }
    }

    public void AddBlob(string blobPath, byte[] content, DateTime uploadedAtUtc) =>
        _blobs[blobPath] = new StoredBlob(content, uploadedAtUtc);

    private static byte[] ReadAllBytes(Stream content)
    {
        if (content is MemoryStream memoryStream)
        {
            return memoryStream.ToArray();
        }

        using var copy = new MemoryStream();
        content.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed record StoredBlob(byte[] Content, DateTime UploadedAtUtc);
}
