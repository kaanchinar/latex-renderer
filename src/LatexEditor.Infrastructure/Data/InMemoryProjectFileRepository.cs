using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Infrastructure.Data;

/// <summary>
/// In-memory <see cref="IProjectFileRepository"/> kept for reference and tests. Not registered in DI.
/// </summary>
public class InMemoryProjectFileRepository: IProjectFileRepository
{
    private readonly List<ProjectFile> _files = [];

    /// <inheritdoc />
    public Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId)
    {
        var files = _files.Where(f => f.ProjectId == projectId).ToList();
        return Task.FromResult<IReadOnlyList<ProjectFile>>(files);
    }

    /// <inheritdoc />
    public Task<ProjectFile?> GetByPathAsync(Guid projectId, string path)
    {
        var file = _files.SingleOrDefault(f => f.ProjectId == projectId && f.Path == path);
        return Task.FromResult(file);
    }

    /// <inheritdoc />
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
        }
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid projectId, string path)
    {
        var file = _files.SingleOrDefault(f => f.ProjectId == projectId && f.Path == path);
        if (file is not null) _files.Remove(file);
        return Task.CompletedTask;
    }
}