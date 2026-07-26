namespace Shora.Application.Abstractions;

public interface ICacheInvalidator
{
    Task InvalidatePublicSettingsAsync(CancellationToken cancellationToken = default);

    Task InvalidateAvailabilityAsync(CancellationToken cancellationToken = default);
}
