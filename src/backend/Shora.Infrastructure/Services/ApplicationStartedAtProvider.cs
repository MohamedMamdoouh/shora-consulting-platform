using Shora.Application.Abstractions;

namespace Shora.Infrastructure.Services;

public sealed class ApplicationStartedAtProvider : IApplicationStartedAtProvider
{
    public DateTime StartedAtUtc { get; } = DateTime.UtcNow;
}
