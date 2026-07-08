namespace LatexEditor.Core.Entities;

public enum StorageProvider
{
    Local,
    S3
}

public class ProjectFile
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string StorageKey { get; set; } = string.Empty;
    public StorageProvider StorageProvider { get; set; }
    public bool IsBinary { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Content { get; set; } = string.Empty;
}