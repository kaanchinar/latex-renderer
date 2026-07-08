using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace LatexEditor.Infrastructure.Data;

public class ProjectFileRepository(AppDbContext db) : IProjectFileRepository
{
    public async Task<IReadOnlyList<ProjectFile>> GetByProjectIdAsync(Guid projectId)
    {
        return await db.ProjectFiles
            .Where(f => f.ProjectId == projectId)
            .ToListAsync();
    }

    public async Task<ProjectFile?> GetByPathAsync(Guid projectId, string path)
    {
        return await db.ProjectFiles
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Path == path);
    }

    public async Task UpsertAsync(ProjectFile file)
    {
        var existing = await db.ProjectFiles
            .FirstOrDefaultAsync(f => f.ProjectId == file.ProjectId && f.Path == file.Path);

        if (existing is null)
        {
            db.ProjectFiles.Add(file);
        }
        else
        {
            existing.Content = file.Content;
            existing.StorageKey = file.StorageKey;
            existing.StorageProvider = file.StorageProvider;
            existing.IsBinary = file.IsBinary;
            existing.UpdatedAt = DateTime.UtcNow;
        }

        await db.SaveChangesAsync();
    }

    public async Task RemoveAsync(Guid projectId, string path)
    {
        var file = await db.ProjectFiles
            .FirstOrDefaultAsync(f => f.ProjectId == projectId && f.Path == path);

        if (file is not null)
        {
            db.ProjectFiles.Remove(file);
            await db.SaveChangesAsync();
        }
    }
}
