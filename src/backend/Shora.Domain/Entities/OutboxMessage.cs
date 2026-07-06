using Shora.Domain.Enums;

namespace Shora.Domain.Entities;

public class OutboxMessage
{
    public Guid Id { get; set; }

    public string MessageType { get; set; } = string.Empty;

    public string AggregateType { get; set; } = string.Empty;

    public Guid AggregateId { get; set; }

    public string IdempotencyKey { get; set; } = string.Empty;

    public string PayloadJson { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; }

    public DateTime NextAttemptAtUtc { get; set; }

    public int AttemptCount { get; set; }

    public OutboxMessageStatus Status { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public string? LastError { get; set; }
}
