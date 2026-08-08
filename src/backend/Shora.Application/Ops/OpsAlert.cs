namespace Shora.Application.Ops;

public enum OpsAlertSeverity
{
    Warning,
    Critical
}

public enum OpsAlertKind
{
    PendingApprovalBacklog,
    CancellationRequestNearAutoDecline,
    RefundDueAgeing,
    JobHeartbeatStale,
    JobFailure,
    OutboxDeadLetter,
    OutboxDeadLetterBurst
}

public sealed record OpsAlert(
    OpsAlertKind Kind,
    OpsAlertSeverity Severity,
    string Message,
    string RunbookId,
    IReadOnlyDictionary<string, string> Context);
