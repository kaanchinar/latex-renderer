using LatexEditor.Core.Entities;

namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Persistence for <see cref="CompileJob"/> entities.
/// </summary>
public interface ICompileJobRepository
{
    /// <summary>Returns the job with the given ID, or <c>null</c> if it does not exist.</summary>
    Task<CompileJob?> GetByIdAsync(Guid id);

    /// <summary>Returns all jobs for the given project, newest first.</summary>
    Task<IReadOnlyList<CompileJob>> GetByProjectIdAsync(Guid projectId);

    /// <summary>Persists a new job.</summary>
    Task AddAsync(CompileJob job);

    /// <summary>Persists changes to an existing job.</summary>
    Task UpdateAsync(CompileJob job);
}
