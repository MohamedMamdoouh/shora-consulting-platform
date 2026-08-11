namespace Shora.Application.Abstractions;

public interface IApplicationStartedAtProvider
{
    DateTime StartedAtUtc { get; }
}
