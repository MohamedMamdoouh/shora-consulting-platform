namespace Shora.Application.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string Endpoint { get; set; } = string.Empty;

    public string AccessKeyId { get; set; } = string.Empty;

    public string SecretAccessKey { get; set; } = string.Empty;

    public string ReceiptBucket { get; set; } = "shora-receipts";

    public int ReceiptReadUrlMinutes { get; set; } = 5;
}
