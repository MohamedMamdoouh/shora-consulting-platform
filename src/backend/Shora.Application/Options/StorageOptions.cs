namespace Shora.Application.Options;

public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    public string ConnectionString { get; set; } = string.Empty;

    public string ReceiptContainer { get; set; } = "receipts";

    public int ReceiptReadUrlMinutes { get; set; } = 5;
}
