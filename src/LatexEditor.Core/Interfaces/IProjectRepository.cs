using LatexEditor.Core.Entities;

namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="Project"/> entities. All reads are owner-scoped.
/// </summary>
public interface IProjectRepository
{
    /// <summary>Returns all projects owned by the given user.</summary>
    Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId);

    /// <summary>Returns the project with the given ID, or <c>null</c> if it does not exist or belongs to another user.</summary>
    Task<Project?> GetByIdAsync(Guid id, string ownerId);

    /// <summary>Persists a new project.</summary>
    Task AddAsync(Project project);

    /// <summary>Persists changes to an existing project.</summary>
    Task UpdateAsync(Project project);

    /// <summary>Deletes a project and (via cascade) its files.</summary>
    Task RemoveAsync(Project project);
}
