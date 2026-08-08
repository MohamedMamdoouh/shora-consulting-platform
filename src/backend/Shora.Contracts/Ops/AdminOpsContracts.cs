namespace Shora.Contracts.Ops;

public sealed record AdminOpsAlertDto(
    string Kind,
    string Severity,
    string Message,
    string RunbookId,
    IReadOnlyDictionary<string, string> Context);

public sealed record AdminOpsAlertsResponse(IReadOnlyList<AdminOpsAlertDto> Alerts);

public sealed record AdminOpsRunbookDto(
    string Id,
    string Owner,
    string ResponseSla,
    string Trigger,
    IReadOnlyList<string> Steps);

public sealed record AdminOpsRunbooksResponse(IReadOnlyList<AdminOpsRunbookDto> Runbooks);
