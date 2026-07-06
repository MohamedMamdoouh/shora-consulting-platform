using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class Booking
{
    public Guid Id { get; set; }

    public Guid ClientId { get; set; }

    public ApplicationUser Client { get; set; } = null!;

    public Guid? AvailabilitySlotId { get; set; }

    public AvailabilitySlot? AvailabilitySlot { get; set; }

    public DateTime SlotStartUtc { get; set; }

    public DateTime SlotEndUtc { get; set; }

    public DeliveryMethod DeliveryMethod { get; set; }

    public string? ContactPhone { get; set; }

    public BookingStatus Status { get; set; }

    public DateTime? ReceiptUploadDeadlineUtc { get; set; }

    public DateTime CreatedAt { get; set; }

    public byte[] RowVersion { get; set; } = [];

    public Payment? Payment { get; set; }

    public CancellationRequest? CancellationRequest { get; set; }

    public ICollection<BookingStatusAudit> StatusAudits { get; set; } = [];
}
