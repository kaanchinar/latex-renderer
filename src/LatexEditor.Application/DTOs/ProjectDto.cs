namespace LatexEditor.Application.DTOs;

public class ProjectDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? LastCompileStatus { get; set; }
    public DateTime CreatedAt { get; set; }
}
