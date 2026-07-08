namespace LatexEditor.Application.DTOs;

public class ProjectFileDto
{
    public Guid Id { get; set; }
    public Guid ProjectId { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public bool IsBinary { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
