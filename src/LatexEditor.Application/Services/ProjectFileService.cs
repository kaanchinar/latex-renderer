using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Services;

public class ProjectFileService(IProjectRepository projectRepo, IProjectFileRepository fileRepo)
{
    public async Task<IReadOnlyList<ProjectFileDto>> GetByProjectIdAsync(Guid projectId, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return [];

        var files = await fileRepo.GetByProjectIdAsync(projectId);
        return files.Select(f => new ProjectFileDto
        {
            Id = f.Id,
            ProjectId = f.ProjectId,
            Path = f.Path,
            Content = f.Content,
            IsBinary = f.IsBinary,
            CreatedAt = f.CreatedAt,
            UpdatedAt = f.UpdatedAt
        }).ToList();
    }

    public async Task<ProjectFileDto?> GetByPathAsync(Guid projectId, string path, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return null;

        var file = await fileRepo.GetByPathAsync(projectId, path);
        if (file is null) return null;

        return new ProjectFileDto
        {
            Id = file.Id,
            ProjectId = file.ProjectId,
            Path = file.Path,
            Content = file.Content,
            IsBinary = file.IsBinary,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        };
    }

    public async Task<ProjectFileDto?> UpsertAsync(Guid projectId, string path, UpsertFileDto dto, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return null;

        var existing = await fileRepo.GetByPathAsync(projectId, path);

        var file = new ProjectFile
        {
            Id = existing?.Id ?? Guid.NewGuid(),
            ProjectId = projectId,
            Path = path,
            Content = dto.Content,
            StorageProvider = StorageProvider.Local,
            StorageKey = $"{projectId}/{path}",
            IsBinary = false,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await fileRepo.UpsertAsync(file);

        return new ProjectFileDto
        {
            Id = file.Id,
            ProjectId = file.ProjectId,
            Path = file.Path,
            Content = file.Content,
            IsBinary = file.IsBinary,
            CreatedAt = file.CreatedAt,
            UpdatedAt = file.UpdatedAt
        };
    }

    public async Task<bool> DeleteAsync(Guid projectId, string path, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return false;

        var file = await fileRepo.GetByPathAsync(projectId, path);
        if (file is null) return false;

        await fileRepo.RemoveAsync(projectId, path);
        return true;
    }
}
