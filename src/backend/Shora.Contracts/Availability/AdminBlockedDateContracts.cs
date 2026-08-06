namespace Shora.Contracts.Availability;

public sealed record BlockedDateResponse(
    Guid Id,
    DateTime StartUtc,
    DateTime EndUtc,
    string? Reason);

public sealed record CreateBlockedDateRequest(
    DateTime StartUtc,
    DateTime EndUtc,
    string? Reason);
