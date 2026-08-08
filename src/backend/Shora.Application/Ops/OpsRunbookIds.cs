namespace Shora.Application.Ops;

public static class OpsRunbookIds
{
    public const string PendingApprovalBacklog = "pending-approval-backlog";

    public const string CancellationRequestNearAutoDecline = "cancellation-request-near-auto-decline";

    public const string RefundDueAgeing = "refund-due-ageing";

    public const string JobHeartbeatMissing = "job-heartbeat-missing";

    public const string JobFailure = "job-failure";

    public const string OutboxDeadLetter = "outbox-dead-letter";

    public const string OutboxDeadLetterBurst = "outbox-dead-letter-burst";
}
