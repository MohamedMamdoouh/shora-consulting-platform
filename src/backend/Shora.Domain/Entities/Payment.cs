using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class Payment
{
    public Guid Id { get; set; }

    public Guid BookingId { get; set; }

    public Booking Booking { get; set; } = null!;

    public PaymentMethod? Method { get; set; }

    public PaymentStatus Status { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "EGP";

    public DateTime? RefundedAtUtc { get; set; }

    public string? RefundReference { get; set; }

    public Guid? RefundedByAdminId { get; set; }

    public ApplicationUser? RefundedByAdmin { get; set; }

    public DateTime? RefundRevokedAtUtc { get; set; }

    public Guid? RefundRevokedByAdminId { get; set; }

    public ApplicationUser? RefundRevokedByAdmin { get; set; }

    public string? RefundRevocationReason { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public ICollection<PaymentReceipt> Receipts { get; set; } = [];
}
