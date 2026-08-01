namespace Shora.Application.Options;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; } = true;

    public int ReceiptUploadDeadlineCleanupIntervalSeconds { get; set; } = 60;

    public int ReceiptRetentionPurgeIntervalSeconds { get; set; } = 86400;

    public int TempBlobCleanupIntervalSeconds { get; set; } = 86400;

    public int TempBlobMaxAgeHours { get; set; } = 24;
}
