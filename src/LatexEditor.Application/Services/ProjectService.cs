using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Services;

public class ProjectService(IProjectRepository repo)
{
    private readonly IProjectRepository _repo = repo;

    public async Task<ProjectDto> CreateAsync(CreateProjectDto dto, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name))
            throw new ArgumentException("Project name is required.", nameof(dto.Name));

        var project = new Project
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            OwnerId = ownerId
        };

        await _repo.AddAsync(project);

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            CreatedAt = project.CreatedAt
        };
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, string ownerId)
    {
        var project = await _repo.GetByIdAsync(id, ownerId);
        if (project is null || project.OwnerId != ownerId) return null;
        return new ProjectDto
        {
            CreatedAt = project.CreatedAt,
            Id = project.Id,
            Name = project.Name,
        };
    }

    public async Task<IReadOnlyList<ProjectDto>> GetByOwnerAsync(string ownerId)
    {
        var projects = await _repo.GetByOwnerAsync(ownerId);
        return [.. projects.Select(p => new ProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            CreatedAt = p.CreatedAt
        })];
    }
}
