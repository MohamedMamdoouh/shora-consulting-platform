namespace Shora.Application.Options;

public sealed class BackgroundJobOptions
{
    public const string SectionName = "BackgroundJobs";

    public bool Enabled { get; set; } = true;

    public int ReceiptUploadDeadlineCleanupIntervalSeconds { get; set; } = 60;

    public int ReceiptRetentionPurgeIntervalSeconds { get; set; } = 86400;

    public int TempBlobCleanupIntervalSeconds { get; set; } = 86400;

    public int TempBlobMaxAgeHours { get; set; } = 24;

    public int OutboxDispatcherIntervalSeconds { get; set; } = 60;

    public int CancellationRequestAutoDeclineIntervalSeconds { get; set; } = 60;

    public int BookingAutoCompleteIntervalSeconds { get; set; } = 300;

    public int RefreshTokenPurgeIntervalSeconds { get; set; } = 86400;

    public int ReceiptBlobReconciliationIntervalSeconds { get; set; } = 900;

    public int ReconciliationTempBlobMaxAgeHours { get; set; } = 1;

    public int AvailabilityTopUpIntervalSeconds { get; set; } = 86400;

    public int OpsMonitoringIntervalSeconds { get; set; } = 300;
}
