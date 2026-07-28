using LatexEditor.Core.Entities;
namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="ProjectFile"/> metadata. File content is handled
/// separately through <see cref="IFileStorage"/>.
/// </summary>
public interface IProjectFileRepository
{
    /// <summary>Returns metadata for all files in the given project.</summary>
    Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId);

    /// <summary>Returns the file at the given project-relative path, or <c>null</c> if it does not exist.</summary>
    Task<ProjectFile?> GetByPathAsync(Guid projectId, string path);

    /// <summary>Inserts the file if the path is new, otherwise updates its metadata.</summary>
    Task UpsertAsync(ProjectFile file);

    /// <summary>Deletes the file metadata at the given path. No-op if it does not exist.</summary>
    Task RemoveAsync(Guid projectId, string path);
}
