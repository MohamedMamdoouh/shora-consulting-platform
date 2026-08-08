using Shora.Application.Common;
using Shora.Application.Options;

namespace Shora.Application.Ops;

internal static class BackgroundJobIntervalRegistry
{
    public static IReadOnlyList<(string JobName, Func<BackgroundJobOptions, int> GetIntervalSeconds)> Jobs { get; } =
    [
        (BackgroundJobNames.ReceiptUploadDeadlineCleanup, options => options.ReceiptUploadDeadlineCleanupIntervalSeconds),
        (BackgroundJobNames.OutboxDispatcher, options => options.OutboxDispatcherIntervalSeconds),
        (BackgroundJobNames.CancellationRequestAutoDecline, options => options.CancellationRequestAutoDeclineIntervalSeconds),
        (BackgroundJobNames.BookingAutoComplete, options => options.BookingAutoCompleteIntervalSeconds),
        (BackgroundJobNames.ReceiptBlobReconciliation, options => options.ReceiptBlobReconciliationIntervalSeconds),
        (BackgroundJobNames.ReceiptRetentionPurge, options => options.ReceiptRetentionPurgeIntervalSeconds),
        (BackgroundJobNames.TempBlobCleanup, options => options.TempBlobCleanupIntervalSeconds),
        (BackgroundJobNames.RefreshTokenPurge, options => options.RefreshTokenPurgeIntervalSeconds),
        (BackgroundJobNames.AvailabilityTopUp, options => options.AvailabilityTopUpIntervalSeconds),
        (BackgroundJobNames.OpsMonitoring, options => options.OpsMonitoringIntervalSeconds)
    ];
}
