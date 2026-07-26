namespace Shora.Application.Abstractions;

public interface ICacheService
{
    Task<T?> GetOrCreateAsync<T>(
        string key,
        Func<CancellationToken, Task<T?>> factory,
        TimeSpan ttl,
        CancellationToken cancellationToken = default);

    void Remove(string key);

    void RemoveByPrefix(string prefix);
}
