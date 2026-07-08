using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Services;

public class ProjectService(IProjectRepository repo)
{
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

        await repo.AddAsync(project);

        return new ProjectDto
        {
            Id = project.Id,
            Name = project.Name,
            CreatedAt = project.CreatedAt
        };
    }

    public async Task<ProjectDto?> GetByIdAsync(Guid id, string ownerId)
    {
        var project = await repo.GetByIdAsync(id, ownerId);
        if (project is null ) return null;
        return new ProjectDto
        {
            CreatedAt = project.CreatedAt,
            Id = project.Id,
            Name = project.Name,
        };
    }

    public async Task<IReadOnlyList<ProjectDto>> GetByOwnerAsync(string ownerId)
    {
        var projects = await repo.GetByOwnerAsync(ownerId);
        return [.. projects.Select(p => new ProjectDto
        {
            Id = p.Id,
            Name = p.Name,
            CreatedAt = p.CreatedAt
        })];
    }

    public async Task<ProjectDto?> UpdateAsync(Guid id, UpdateProjectDto dto, string ownerId)
    {
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new ArgumentException("Project name is required.", nameof(dto.Name));

        var project = await repo.GetByIdAsync(id, ownerId);
        if (project is null ) return null;

        project.Name = dto.Name;
        await repo.UpdateAsync(project);
        return new ProjectDto
        {
            Id = project.Id,
            CreatedAt = project.CreatedAt,
            Name = project.Name,
        };

    }

    public async Task<bool> DeleteAsync(Guid id, string ownerId)
    {
        var project = await repo.GetByIdAsync(id, ownerId);
        if (project is null) return false;
        await repo.RemoveAsync(project);
        return true;
    }
}
