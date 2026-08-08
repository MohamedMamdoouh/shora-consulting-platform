namespace Shora.Application.Common;

public static class BackgroundJobNames
{
    public const string ReceiptUploadDeadlineCleanup = nameof(ReceiptUploadDeadlineCleanup);

    public const string ReceiptRetentionPurge = nameof(ReceiptRetentionPurge);

    public const string TempBlobCleanup = nameof(TempBlobCleanup);

    public const string OutboxDispatcher = nameof(OutboxDispatcher);

    public const string CancellationRequestAutoDecline = nameof(CancellationRequestAutoDecline);

    public const string BookingAutoComplete = nameof(BookingAutoComplete);

    public const string RefreshTokenPurge = nameof(RefreshTokenPurge);

    public const string ReceiptBlobReconciliation = nameof(ReceiptBlobReconciliation);

    public const string AvailabilityTopUp = nameof(AvailabilityTopUp);

    public const string OpsMonitoring = nameof(OpsMonitoring);
}
