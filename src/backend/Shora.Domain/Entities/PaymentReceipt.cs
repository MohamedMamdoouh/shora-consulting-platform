using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class PaymentReceipt
{
    public Guid Id { get; set; }

    public Guid PaymentId { get; set; }

    public Payment Payment { get; set; } = null!;

    public string BlobPath { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string ContentHashSha256 { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    public string? SenderReference { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public BlobState BlobState { get; set; }

    public MalwareScanStatus MalwareScanStatus { get; set; }

    public ReceiptReviewStatus ReviewStatus { get; set; }

    public Guid? ReviewedByAdminId { get; set; }

    public ApplicationUser? ReviewedByAdmin { get; set; }

    public DateTime? ReviewedAtUtc { get; set; }

    public DeclineReasonCode? DeclineReasonCode { get; set; }

    public string? DeclineReason { get; set; }
}
