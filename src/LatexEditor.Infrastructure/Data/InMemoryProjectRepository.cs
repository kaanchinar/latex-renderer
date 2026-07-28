using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Infrastructure.Data;

/// <summary>
/// In-memory <see cref="IProjectRepository"/> kept for reference and tests. Not registered in DI.
/// </summary>
public class InMemoryProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects = new();

    /// <inheritdoc />
    public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId)
    {
        var projects = _projects.Where(p => p.OwnerId == ownerId).ToList();
        return Task.FromResult<IReadOnlyList<Project>>(projects);
    }

    /// <inheritdoc />
    public Task<Project?> GetByIdAsync(Guid id, string ownerId)
    {
        var project = _projects.FirstOrDefault(p => p.Id == id && p.OwnerId == ownerId);
        return Task.FromResult(project);
    }

    /// <inheritdoc />
    public Task AddAsync(Project project)
    {
        _projects.Add(project);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task UpdateAsync(Project project)
    {
        var existingProject = _projects.FirstOrDefault(p => p.Id == project.Id && p.OwnerId == project.OwnerId);
        if (existingProject is null) return Task.CompletedTask;
        existingProject.Name = project.Name;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Project project)
    {
        var existingProject = _projects.FirstOrDefault(p => p.Id == project.Id && p.OwnerId == project.OwnerId);
        if (existingProject is null) return Task.CompletedTask;
        _projects.Remove(existingProject);
        return Task.CompletedTask;
    }
}
