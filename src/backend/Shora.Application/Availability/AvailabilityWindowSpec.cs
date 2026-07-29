namespace Shora.Application.Availability;

public sealed record AvailabilityWindowSpec(
    DayOfWeek DayOfWeek,
    TimeSpan StartTime,
    TimeSpan EndTime,
    bool IsActive = true);
