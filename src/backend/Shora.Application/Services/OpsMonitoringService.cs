using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shora.Application.Abstractions;
using Shora.Application.Ops;
using Shora.Application.Options;
using Shora.Domain.Enums;

namespace Shora.Application.Services;

public sealed class OpsMonitoringService(
    IApplicationDbContext dbContext,
    IDateTimeProvider dateTimeProvider,
    IApplicationStartedAtProvider applicationStartedAtProvider,
    IOptions<OpsMonitoringOptions> opsMonitoringOptions,
    IOptions<BackgroundJobOptions> backgroundJobOptions,
    ILogger<OpsMonitoringService> logger)
{
    public async Task<IReadOnlyList<OpsAlert>> EvaluateAlertsAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider.UtcNow;
        var options = opsMonitoringOptions.Value;
        var alerts = new List<OpsAlert>();

        alerts.AddRange(await EvaluatePendingApprovalBacklogAsync(now, options, cancellationToken));
        alerts.AddRange(await EvaluateCancellationRequestsAsync(now, options, cancellationToken));
        alerts.AddRange(await EvaluateRefundDueAgeingAsync(now, options, cancellationToken));
        alerts.AddRange(await EvaluateOutboxAlertsAsync(now, options, cancellationToken));

        if (backgroundJobOptions.Value.Enabled)
        {
            alerts.AddRange(await EvaluateJobAlertsAsync(now, options, backgroundJobOptions.Value, cancellationToken));
        }

        LogAlerts(alerts);
        return alerts;
    }

    private async Task<List<OpsAlert>> EvaluatePendingApprovalBacklogAsync(
        DateTime now,
        OpsMonitoringOptions options,
        CancellationToken cancellationToken)
    {
        var warningThreshold = now.AddHours(-options.PendingApprovalWarningHours);
        var criticalThreshold = now.AddHours(-options.PendingApprovalCriticalHours);

        var pendingBookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking => booking.Status == BookingStatus.PendingApproval)
            .Select(booking => new
            {
                booking.Id,
                PendingSinceUtc = booking.StatusAudits
                    .Where(audit => audit.ToStatus == BookingStatus.PendingApproval)
                    .Max(audit => (DateTime?)audit.AtUtc)
            })
            .ToListAsync(cancellationToken);

        var alerts = new List<OpsAlert>();

        foreach (var booking in pendingBookings)
        {
            var pendingSinceUtc = booking.PendingSinceUtc;
            if (pendingSinceUtc is null)
            {
                continue;
            }

            if (pendingSinceUtc <= criticalThreshold)
            {
                alerts.Add(CreateAlert(
                    OpsAlertKind.PendingApprovalBacklog,
                    OpsAlertSeverity.Critical,
                    OpsRunbookIds.PendingApprovalBacklog,
                    $"Booking {booking.Id} has been PendingApproval for more than {options.PendingApprovalCriticalHours} hours.",
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = booking.Id.ToString(),
                        ["pendingSinceUtc"] = pendingSinceUtc.Value.ToString("O"),
                        ["ageHours"] = (now - pendingSinceUtc.Value).TotalHours.ToString("F1")
                    }));
            }
            else if (pendingSinceUtc <= warningThreshold)
            {
                alerts.Add(CreateAlert(
                    OpsAlertKind.PendingApprovalBacklog,
                    OpsAlertSeverity.Warning,
                    OpsRunbookIds.PendingApprovalBacklog,
                    $"Booking {booking.Id} has been PendingApproval for more than {options.PendingApprovalWarningHours} hours.",
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = booking.Id.ToString(),
                        ["pendingSinceUtc"] = pendingSinceUtc.Value.ToString("O"),
                        ["ageHours"] = (now - pendingSinceUtc.Value).TotalHours.ToString("F1")
                    }));
            }
        }

        return alerts;
    }

    private async Task<List<OpsAlert>> EvaluateCancellationRequestsAsync(
        DateTime now,
        OpsMonitoringOptions options,
        CancellationToken cancellationToken)
    {
        var warningDeadline = now.AddMinutes(options.CancellationRequestWarningMinutes);

        var requests = await dbContext.CancellationRequests
            .AsNoTracking()
            .Where(request =>
                request.Status == CancellationRequestStatus.Pending
                && request.AutoDeclineAtUtc <= warningDeadline
                && request.AutoDeclineAtUtc > now)
            .Select(request => new
            {
                request.Id,
                request.BookingId,
                request.AutoDeclineAtUtc
            })
            .ToListAsync(cancellationToken);

        return requests
            .Select(request => CreateAlert(
                OpsAlertKind.CancellationRequestNearAutoDecline,
                OpsAlertSeverity.Warning,
                OpsRunbookIds.CancellationRequestNearAutoDecline,
                $"Cancellation request {request.Id} for booking {request.BookingId} auto-declines within {options.CancellationRequestWarningMinutes} minutes.",
                new Dictionary<string, string>
                {
                    ["cancellationRequestId"] = request.Id.ToString(),
                    ["bookingId"] = request.BookingId.ToString(),
                    ["autoDeclineAtUtc"] = request.AutoDeclineAtUtc.ToString("O"),
                    ["minutesRemaining"] = (request.AutoDeclineAtUtc - now).TotalMinutes.ToString("F0")
                }))
            .ToList();
    }

    private async Task<List<OpsAlert>> EvaluateRefundDueAgeingAsync(
        DateTime now,
        OpsMonitoringOptions options,
        CancellationToken cancellationToken)
    {
        var warningThreshold = now.AddHours(-options.RefundDueWarningHours);
        var criticalThreshold = now.AddHours(-options.RefundDueCriticalHours);

        var refundDueBookings = await dbContext.Bookings
            .AsNoTracking()
            .Where(booking =>
                booking.Status == BookingStatus.Cancelled
                && booking.Payment != null
                && booking.Payment.Status == PaymentStatus.Approved)
            .Select(booking => new
            {
                booking.Id,
                PaymentId = booking.Payment!.Id,
                CancelledAtUtc = booking.StatusAudits
                    .Where(audit => audit.ToStatus == BookingStatus.Cancelled)
                    .Max(audit => (DateTime?)audit.AtUtc)
            })
            .ToListAsync(cancellationToken);

        var alerts = new List<OpsAlert>();

        foreach (var booking in refundDueBookings)
        {
            var cancelledAtUtc = booking.CancelledAtUtc;
            if (cancelledAtUtc is null)
            {
                continue;
            }

            if (cancelledAtUtc <= criticalThreshold)
            {
                alerts.Add(CreateAlert(
                    OpsAlertKind.RefundDueAgeing,
                    OpsAlertSeverity.Critical,
                    OpsRunbookIds.RefundDueAgeing,
                    $"Refund due for payment {booking.PaymentId} has aged more than {options.RefundDueCriticalHours} hours.",
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = booking.Id.ToString(),
                        ["paymentId"] = booking.PaymentId.ToString(),
                        ["cancelledAtUtc"] = cancelledAtUtc.Value.ToString("O"),
                        ["ageHours"] = (now - cancelledAtUtc.Value).TotalHours.ToString("F1")
                    }));
            }
            else if (cancelledAtUtc <= warningThreshold)
            {
                alerts.Add(CreateAlert(
                    OpsAlertKind.RefundDueAgeing,
                    OpsAlertSeverity.Warning,
                    OpsRunbookIds.RefundDueAgeing,
                    $"Refund due for payment {booking.PaymentId} has aged more than {options.RefundDueWarningHours} hours.",
                    new Dictionary<string, string>
                    {
                        ["bookingId"] = booking.Id.ToString(),
                        ["paymentId"] = booking.PaymentId.ToString(),
                        ["cancelledAtUtc"] = cancelledAtUtc.Value.ToString("O"),
                        ["ageHours"] = (now - cancelledAtUtc.Value).TotalHours.ToString("F1")
                    }));
            }
        }

        return alerts;
    }

    private async Task<List<OpsAlert>> EvaluateOutboxAlertsAsync(
        DateTime now,
        OpsMonitoringOptions options,
        CancellationToken cancellationToken)
    {
        var alerts = new List<OpsAlert>();

        var deadLetteredMessages = await dbContext.OutboxMessages
            .AsNoTracking()
            .Where(message => message.Status == OutboxMessageStatus.DeadLettered)
            .Select(message => new
            {
                message.Id,
                message.MessageType,
                message.AggregateId,
                message.LastError
            })
            .ToListAsync(cancellationToken);

        foreach (var message in deadLetteredMessages)
        {
            alerts.Add(CreateAlert(
                OpsAlertKind.OutboxDeadLetter,
                OpsAlertSeverity.Warning,
                OpsRunbookIds.OutboxDeadLetter,
                $"Outbox message {message.Id} ({message.MessageType}) is dead-lettered.",
                new Dictionary<string, string>
                {
                    ["messageId"] = message.Id.ToString(),
                    ["messageType"] = message.MessageType,
                    ["aggregateId"] = message.AggregateId.ToString(),
                    ["lastError"] = message.LastError ?? string.Empty
                }));
        }

        var burstWindowStart = now.AddHours(-options.OutboxDeadLetterBurstWindowHours);
        var recentDeadLetterCount = await dbContext.OutboxMessages
            .AsNoTracking()
            .CountAsync(
                message => message.Status == OutboxMessageStatus.DeadLettered
                           && message.CreatedAtUtc >= burstWindowStart,
                cancellationToken);

        if (recentDeadLetterCount >= options.OutboxDeadLetterBurstCount)
        {
            alerts.Add(CreateAlert(
                OpsAlertKind.OutboxDeadLetterBurst,
                OpsAlertSeverity.Critical,
                OpsRunbookIds.OutboxDeadLetterBurst,
                $"{recentDeadLetterCount} outbox messages dead-lettered within the last {options.OutboxDeadLetterBurstWindowHours} hour(s).",
                new Dictionary<string, string>
                {
                    ["deadLetterCount"] = recentDeadLetterCount.ToString(),
                    ["windowHours"] = options.OutboxDeadLetterBurstWindowHours.ToString()
                }));
        }

        return alerts;
    }

    private async Task<List<OpsAlert>> EvaluateJobAlertsAsync(
        DateTime now,
        OpsMonitoringOptions options,
        BackgroundJobOptions jobOptions,
        CancellationToken cancellationToken)
    {
        var uptime = now - applicationStartedAtProvider.StartedAtUtc;
        if (uptime < TimeSpan.FromMinutes(options.JobHeartbeatStartupGraceMinutes))
        {
            logger.LogDebug(
                "Skipping job heartbeat alerts during startup grace ({ElapsedMinutes:F1}/{GraceMinutes} min).",
                uptime.TotalMinutes,
                options.JobHeartbeatStartupGraceMinutes);
            return [];
        }

        var alerts = new List<OpsAlert>();
        var histories = await dbContext.JobRunHistories
            .AsNoTracking()
            .ToDictionaryAsync(history => history.JobName, cancellationToken);

        foreach (var (jobName, getIntervalSeconds) in BackgroundJobIntervalRegistry.Jobs)
        {
            var intervalSeconds = getIntervalSeconds(jobOptions);
            if (intervalSeconds <= 0)
            {
                continue;
            }

            histories.TryGetValue(jobName, out var history);
            var warningThreshold = TimeSpan.FromSeconds(intervalSeconds * options.JobHeartbeatWarningIntervals);
            var criticalThreshold = TimeSpan.FromSeconds(intervalSeconds * options.JobHeartbeatCriticalIntervals);

            if (history?.LastSuccessAtUtc is DateTime lastSuccessAtUtc)
            {
                var staleness = now - lastSuccessAtUtc;
                if (staleness > criticalThreshold)
                {
                    alerts.Add(CreateJobHeartbeatAlert(
                        jobName,
                        OpsAlertSeverity.Critical,
                        staleness,
                        options.JobHeartbeatCriticalIntervals,
                        intervalSeconds));
                }
                else if (staleness > warningThreshold)
                {
                    alerts.Add(CreateJobHeartbeatAlert(
                        jobName,
                        OpsAlertSeverity.Warning,
                        staleness,
                        options.JobHeartbeatWarningIntervals,
                        intervalSeconds));
                }
            }
            else if (history?.LastFailureAtUtc is null)
            {
                alerts.Add(CreateJobHeartbeatAlert(
                    jobName,
                    OpsAlertSeverity.Warning,
                    null,
                    options.JobHeartbeatWarningIntervals,
                    intervalSeconds,
                    neverSucceeded: true));
            }

            if (history?.LastFailureAtUtc is DateTime lastFailureAtUtc
                && (history.LastSuccessAtUtc is null || lastFailureAtUtc > history.LastSuccessAtUtc))
            {
                alerts.Add(CreateAlert(
                    OpsAlertKind.JobFailure,
                    OpsAlertSeverity.Warning,
                    OpsRunbookIds.JobFailure,
                    $"Background job {jobName} recorded a failure at {lastFailureAtUtc:O}.",
                    new Dictionary<string, string>
                    {
                        ["jobName"] = jobName,
                        ["lastFailureAtUtc"] = lastFailureAtUtc.ToString("O"),
                        ["lastError"] = history.LastError ?? string.Empty
                    }));
            }
        }

        return alerts;
    }

    private static OpsAlert CreateJobHeartbeatAlert(
        string jobName,
        OpsAlertSeverity severity,
        TimeSpan? staleness,
        int intervalMultiplier,
        int intervalSeconds,
        bool neverSucceeded = false)
    {
        var message = neverSucceeded
            ? $"Background job {jobName} has no successful heartbeat yet."
            : $"Background job {jobName} last succeeded {staleness!.Value.TotalMinutes:F0} minutes ago (> {intervalMultiplier} expected intervals).";

        return CreateAlert(
            OpsAlertKind.JobHeartbeatStale,
            severity,
            OpsRunbookIds.JobHeartbeatMissing,
            message,
            new Dictionary<string, string>
            {
                ["jobName"] = jobName,
                ["intervalSeconds"] = intervalSeconds.ToString(),
                ["intervalMultiplier"] = intervalMultiplier.ToString(),
                ["stalenessMinutes"] = staleness?.TotalMinutes.ToString("F0") ?? "unknown"
            });
    }

    private static OpsAlert CreateAlert(
        OpsAlertKind kind,
        OpsAlertSeverity severity,
        string runbookId,
        string message,
        IReadOnlyDictionary<string, string> context) =>
        new(kind, severity, message, runbookId, context);

    private void LogAlerts(IReadOnlyList<OpsAlert> alerts)
    {
        foreach (var alert in alerts)
        {
            if (alert.Severity == OpsAlertSeverity.Critical)
            {
                logger.LogError(
                    "Ops alert {AlertKind} ({RunbookId}): {Message} Context={@Context}",
                    alert.Kind,
                    alert.RunbookId,
                    alert.Message,
                    alert.Context);
            }
            else
            {
                logger.LogWarning(
                    "Ops alert {AlertKind} ({RunbookId}): {Message} Context={@Context}",
                    alert.Kind,
                    alert.RunbookId,
                    alert.Message,
                    alert.Context);
            }
        }

        if (alerts.Count > 0)
        {
            logger.LogInformation(
                "Ops monitoring evaluated {AlertCount} active alert(s).",
                alerts.Count);
        }
    }
}
