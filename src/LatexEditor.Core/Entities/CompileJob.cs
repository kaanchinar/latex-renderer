namespace LatexEditor.Core.Entities;

public enum CompileStatus
{
    Queued,
    Running,
    Success,
    Failed,
    Cancelled
}

public class CompileJob
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public CompileStatus Status { get; set; } = CompileStatus.Queued;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public string StdOut { get; set; } = string.Empty;
    public string StdErr { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public string? OutputStorageKey { get; set; }
}
