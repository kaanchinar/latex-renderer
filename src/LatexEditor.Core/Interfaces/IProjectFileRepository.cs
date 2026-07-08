using LatexEditor.Core.Entities;
namespace LatexEditor.Core.Interfaces;

public interface IProjectFileRepository
{
    Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId);
    Task<ProjectFile?> GetByPathAsync(Guid projectId ,string path);
    Task UpsertAsync(ProjectFile file);
    Task RemoveAsync(Guid projectId, string path);
}