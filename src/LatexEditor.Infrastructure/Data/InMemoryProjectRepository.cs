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
}
