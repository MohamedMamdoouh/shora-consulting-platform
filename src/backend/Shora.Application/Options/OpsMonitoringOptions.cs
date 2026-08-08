namespace Shora.Application.Options;

public sealed class OpsMonitoringOptions
{
    public const string SectionName = "OpsMonitoring";

    public int PendingApprovalWarningHours { get; set; } = 6;

    public int PendingApprovalCriticalHours { get; set; } = 24;

    public int CancellationRequestWarningMinutes { get; set; } = 30;

    public int RefundDueWarningHours { get; set; } = 24;

    public int RefundDueCriticalHours { get; set; } = 72;

    public int JobHeartbeatWarningIntervals { get; set; } = 2;

    public int JobHeartbeatCriticalIntervals { get; set; } = 4;

    public int OutboxDeadLetterBurstCount { get; set; } = 5;

    public int OutboxDeadLetterBurstWindowHours { get; set; } = 1;
}
