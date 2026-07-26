namespace Shora.Domain.Entities;

public class AvailabilitySlot
{
    public Guid Id { get; set; }

    public DateTime StartTimeUtc { get; set; }

    public DateTime EndTimeUtc { get; set; }

    public bool IsBooked { get; set; }

    public Guid? BookingId { get; set; }

    public Booking? Booking { get; set; }
}
