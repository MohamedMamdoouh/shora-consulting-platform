namespace Shora.Domain.Entities;

public class AvailabilityWindow
{
    public Guid Id { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan StartTime { get; set; }

    public TimeSpan EndTime { get; set; }

    public bool IsActive { get; set; } = true;
}
