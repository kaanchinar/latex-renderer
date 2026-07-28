using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LatexEditor.Infrastructure.Data;

/// <summary>EF Core / PostgreSQL implementation of <see cref="ICompileJobRepository"/>.</summary>
public class CompileJobRepository(AppDbContext db) : ICompileJobRepository
{
    /// <inheritdoc />
    public async Task<CompileJob?> GetByIdAsync(Guid id)
    {
        return await db.CompileJobs.FirstOrDefaultAsync(j => j.Id == id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<CompileJob>> GetByProjectIdAsync(Guid projectId)
    {
        return await db.CompileJobs
            .Where(j => j.ProjectId == projectId)
            .OrderByDescending(j => j.CreatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddAsync(CompileJob job)
    {
        db.CompileJobs.Add(job);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(CompileJob job)
    {
        db.CompileJobs.Update(job);
        await db.SaveChangesAsync();
    }
}
