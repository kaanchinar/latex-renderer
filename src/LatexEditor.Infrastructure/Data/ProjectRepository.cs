using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LatexEditor.Infrastructure.Data;

public class ProjectRepository(AppDbContext db) : IProjectRepository
{
    public async Task<Project?> GetByIdAsync(Guid id, string ownerId)
    {
        return await db.Projects
            .FirstOrDefaultAsync(p => p.Id == id && p.OwnerId == ownerId);
    }

    public async Task<IReadOnlyList<Project>> GetByOwnerAsync(string ownerId)
    {
        return await db.Projects
            .Where(p => p.OwnerId == ownerId)
            .ToListAsync();
    }

    public async Task AddAsync(Project project)
    {
        db.Projects.Add(project);
        await db.SaveChangesAsync();
    }

    public async Task UpdateAsync(Project project)
    {
        project.UpdatedAt = DateTime.UtcNow;
        db.Projects.Update(project);
        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(Project project)
    {
        db.Projects.Remove(project);
        await db.SaveChangesAsync();
    }
}
