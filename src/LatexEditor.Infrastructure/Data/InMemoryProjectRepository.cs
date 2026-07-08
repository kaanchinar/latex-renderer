using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Infrastructure.Data;

public class InMemoryProjectRepository : IProjectRepository
{
    private readonly List<Project> _projects = new();

    public Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId)
    {
        var projects = _projects.Where(p => p.OwnerId == ownerId).ToList();
        return Task.FromResult<IReadOnlyList<Project>>(projects);
    }

    public Task<Project?> GetByIdAsync(Guid id, string ownerId)
    {
        var project = _projects.FirstOrDefault(p => p.Id == id && p.OwnerId == ownerId);
        return Task.FromResult(project);
    }

    public Task AddAsync(Project project)
    {
        _projects.Add(project);
        return Task.CompletedTask;
    }

    public Task UpdateAsync(Project project)
    {
        var existingProject = _projects.FirstOrDefault(p => p.Id == project.Id && p.OwnerId == project.OwnerId);
        if (existingProject is null) return Task.CompletedTask;
        existingProject.Name = project.Name;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Project project)
    {
        var existingProject = _projects.FirstOrDefault(p => p.Id == project.Id && p.OwnerId == project.OwnerId);
        if (existingProject is null) return Task.CompletedTask;
        _projects.Remove(existingProject);
        return Task.CompletedTask;
    }
}
