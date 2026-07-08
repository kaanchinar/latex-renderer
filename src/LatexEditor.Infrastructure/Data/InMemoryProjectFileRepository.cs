using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Infrastructure.Data;

public class InMemoryProjectFileRepository: IProjectFileRepository
{
    private readonly List<ProjectFile> _files = [];

    public Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId)
    {
        var files = _files.Where(f => f.ProjectId == projectId).ToList();
        return Task.FromResult<IReadOnlyList<ProjectFile>>(files);
    }

    public Task<ProjectFile?> GetByPathAsync(Guid projectId, string path)
    {
        var file = _files.SingleOrDefault(f => f.ProjectId == projectId && f.Path == path);
        return Task.FromResult(file);
    }

    public Task UpsertAsync(ProjectFile file)
    {
        var existing = _files.SingleOrDefault(f => f.ProjectId == file.ProjectId && f.Path == file.Path);
        if (existing is null) _files.Add(file);
        else
        {
            existing.UpdatedAt = file.UpdatedAt;
            existing.IsBinary = file.IsBinary;
            existing.StorageKey = file.StorageKey;
            existing.StorageProvider = file.StorageProvider;
            existing.Content = file.Content;
        }
        return Task.CompletedTask;
    }

    public Task RemoveAsync(Guid projectId, string path)
    {
        var file = _files.SingleOrDefault(f => f.ProjectId == projectId && f.Path == path);
        if (file is not null) _files.Remove(file);
        return Task.CompletedTask;
    }
}