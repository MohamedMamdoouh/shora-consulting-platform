namespace Shora.Domain.Entities;

public class AvailabilitySlot
{
    public Guid Id { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime EndTime { get; set; }

    public bool IsBooked { get; set; }

    public Guid? BookingId { get; set; }

    public Booking? Booking { get; set; }
}
