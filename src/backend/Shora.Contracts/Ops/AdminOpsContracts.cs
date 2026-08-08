namespace Shora.Contracts.Ops;

public sealed record AdminOpsAlertDto(
    string Kind,
    string Severity,
    string Message,
    string RunbookId,
    IReadOnlyDictionary<string, string> Context);

public sealed record AdminOpsAlertsResponse(IReadOnlyList<AdminOpsAlertDto> Alerts);
