using LatexEditor.Core.Entities;

namespace LatexEditor.Core.Interfaces;

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId);
    Task<Project?> GetByIdAsync(Guid id,  string ownerId);
    Task AddAsync(Project project);
    Task UpdateAsync(Project project);
    Task RemoveAsync(Project project);
}
