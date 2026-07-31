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
            Slug = await GenerateUniqueSlugAsync(ownerId, GenerateSlug(dto.Name)),
            OwnerId = ownerId
        };

        await repo.AddAsync(project);

        return Map(project);
    }

    /// <summary>Returns the project, or <c>null</c> if it does not exist or belongs to another user.</summary>
    public async Task<ProjectDto?> GetByIdAsync(Guid id, string ownerId)
    {
        var project = await repo.GetByIdAsync(id, ownerId);
        if (project is null ) return null;
        return Map(project);
    }

    /// <summary>Returns all projects owned by the given user.</summary>
    public async Task<IReadOnlyList<ProjectDto>> GetByOwnerAsync(string ownerId)
    {
        var projects = await repo.GetByOwnerAsync(ownerId);
        return [.. projects.Select(Map)];
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
        return Map(project);
    }

    /// <summary>Deletes the project. Returns <c>false</c> if it does not exist or belongs to another user.</summary>
    public async Task<bool> DeleteAsync(Guid id, string ownerId)
    {
        var project = await repo.GetByIdAsync(id, ownerId);
        if (project is null) return false;
        await repo.RemoveAsync(project);
        return true;
    }

    private static ProjectDto Map(Project project) => new()
    {
        Id = project.Id,
        Name = project.Name,
        Slug = project.Slug,
        LastCompileStatus = project.LastCompileStatus?.ToString(),
        CreatedAt = project.CreatedAt
    };

    /// <summary>
    /// Converts a project name into a URL-friendly slug: lowercased, non-alphanumeric
    /// runs replaced by single dashes, dashes trimmed from the ends.
    /// </summary>
    public static string GenerateSlug(string name)
    {
        var slug = name.Trim().ToLowerInvariant();
        var builder = new System.Text.StringBuilder(slug.Length);
        var lastWasDash = false;

        foreach (var c in slug)
        {
            if (char.IsLetterOrDigit(c))
            {
                builder.Append(c);
                lastWasDash = false;
            }
            else if (!lastWasDash && builder.Length > 0)
            {
                builder.Append('-');
                lastWasDash = true;
            }
        }

        return builder.ToString().TrimEnd('-');
    }

    private async Task<string> GenerateUniqueSlugAsync(string ownerId, string baseSlug)
    {
        var existing = await repo.GetByOwnerAsync(ownerId);
        var existingSlugs = existing.Select(p => p.Slug).ToHashSet();
        if (!existingSlugs.Contains(baseSlug)) return baseSlug;

        var suffix = 2;
        while (true)
        {
            var candidate = $"{baseSlug}-{suffix}";
            if (!existingSlugs.Contains(candidate)) return candidate;
            suffix++;
        }
    }
}
