namespace Shora.Application.Options;

public sealed class ReceiptUploadOptions
{
    public const string SectionName = "ReceiptUpload";

    public int MaxSizeBytes { get; set; } = 5 * 1024 * 1024;

    public int RateLimitPermitLimit { get; set; } = 5;

    public int RateLimitWindowMinutes { get; set; } = 1;
}
