using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Services;

/// <summary>
/// Use-case orchestration for project CRUD. All operations are scoped to the
/// authenticated owner; non-existent or foreign projects surface as <c>null</c>/<c>false</c>.
/// </summary>
public class ProjectService(IProjectRepository repo)
{
    /// <summary>Creates a new project owned by the given user.</summary>
    /// <exception cref="ArgumentException">The project name is empty or whitespace.</exception>
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

    /// <summary>Returns the project, or <c>null</c> if it does not exist or belongs to another user.</summary>
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

    /// <summary>Returns all projects owned by the given user.</summary>
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

    /// <summary>Renames the project, or returns <c>null</c> if it does not exist or belongs to another user.</summary>
    /// <exception cref="ArgumentException">The project name is empty or whitespace.</exception>
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

    /// <summary>Deletes the project. Returns <c>false</c> if it does not exist or belongs to another user.</summary>
    public async Task<bool> DeleteAsync(Guid id, string ownerId)
    {
        var project = await repo.GetByIdAsync(id, ownerId);
        if (project is null) return false;
        await repo.RemoveAsync(project);
        return true;
    }
}
