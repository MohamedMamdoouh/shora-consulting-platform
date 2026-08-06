namespace Shora.Contracts.Availability;

public sealed record AvailabilityWindowResponse(
    Guid Id,
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive);

public sealed record CreateAvailabilityWindowRequest(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive = true);

public sealed record UpdateAvailabilityWindowRequest(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive);
