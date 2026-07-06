namespace Shora.Domain.Entities;

public class BlockedDate
{
    public Guid Id { get; set; }

    public DateTime StartUtc { get; set; }

    public DateTime EndUtc { get; set; }

    public string? Reason { get; set; }
}
