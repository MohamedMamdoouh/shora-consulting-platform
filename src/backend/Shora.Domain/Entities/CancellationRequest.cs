using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class CancellationRequest
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Booking Booking { get; set; } = null!;

    public Guid RequestedByClientId { get; set; }

    public ApplicationUser RequestedByClient { get; set; } = null!;

    public DateTime RequestedAtUtc { get; set; }

    public string? ClientReason { get; set; }

    public DateTime AutoDeclineAtUtc { get; set; }

    public CancellationRequestStatus Status { get; set; }

    public int ReopenCount { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public ApplicationUser? ReviewedByAdmin { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DecisionReasonCode? DecisionReasonCode { get; set; }

    public string? DecisionReason { get; set; }

    public DateTime? ClientDecisionSeenAtUtc { get; set; }
}
