using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class BookingStatusAudit
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Booking Booking { get; set; } = null!;

    public BookingStatus? FromStatus { get; set; }

    public BookingStatus ToStatus { get; set; }

    public AuditActor Actor { get; set; }

    public Guid? ActorUserId { get; set; }

    public ApplicationUser? ActorUser { get; set; }

    public string? Reason { get; set; }

    public DateTime AtUtc { get; set; }
}
