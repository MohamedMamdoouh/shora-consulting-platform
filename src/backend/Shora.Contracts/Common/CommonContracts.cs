namespace Shora.Contracts.Common;

public sealed record MessageResponse(string Message);

public sealed record HealthResponse(string Status, DateTime TimestampUtc);
