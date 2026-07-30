namespace Shora.Application.Options;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; } = true;

    public int ReceiptUploadDeadlineCleanupIntervalSeconds { get; set; } = 60;
}
