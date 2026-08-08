namespace Shora.Domain.Entities;

public class JobRunHistory
{
    public string JobName { get; set; } = string.Empty;

    public DateTime? LastSuccessAtUtc { get; set; }

    public DateTime? LastFailureAtUtc { get; set; }

    public string? LastError { get; set; }
}
