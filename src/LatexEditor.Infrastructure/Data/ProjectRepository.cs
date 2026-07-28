using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LatexEditor.Infrastructure.Data;

/// <summary>EF Core / PostgreSQL implementation of <see cref="IProjectRepository"/>.</summary>
public class ProjectRepository(AppDbContext db) : IProjectRepository
{
    /// <inheritdoc />
    public async Task<Project?> GetByIdAsync(Guid id, string ownerId)
    {
        return await db.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
    }

    /// <inheritdoc />
    public async Task<Project?> GetByIdUnrestrictedAsync(Guid id)
    {
        return await db.Projects.FirstOrDefaultAsync(p => p.Id == id);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId)
    {
        return await db.Projects
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task AddAsync(Project project)
    {
        db.Projects.Add(project);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task UpdateAsync(Project project)
    {
        project.UpdatedAt = DateTime.UtcNow;
        db.Projects.Update(project);
        await db.SaveChangesAsync();
    }

    /// <inheritdoc />
    public async Task RemoveAsync(Project project)
    {
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
    }
}
