namespace Shora.Application.Abstractions;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
