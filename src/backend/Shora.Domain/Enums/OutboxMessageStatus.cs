namespace Shora.Domain.Enums;

public enum OutboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    DeadLettered = 2
}
