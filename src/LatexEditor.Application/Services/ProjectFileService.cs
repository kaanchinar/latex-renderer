using System.Text;
using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Services;

/// <summary>
/// Use-case orchestration for project files. Metadata lives in PostgreSQL via
/// <see cref="IProjectFileRepository"/>; content lives in object storage via
/// <see cref="IFileStorage"/>. All operations are scoped to the authenticated owner.
/// </summary>
public class ProjectFileService(IProjectRepository projectRepo, IProjectFileRepository fileRepo, IFileStorage storage)
{
    /// <summary>
    /// Returns metadata for all files in the project. Content is not included;
    /// use <see cref="GetByPathAsync"/> to read a single file's content.
    /// </summary>
    public async Task<IReadOnlyList<ProjectFileDto>> GetByProjectIdAsync(Guid projectId, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return [];

        var files = await fileRepo.GetByProjectIdAsync(projectId);
        return files.Select(MapMetadata).ToList();
    }

    /// <summary>
    /// Returns the file at the given path including its text content,
    /// or <c>null</c> if the project or file does not exist or belongs to another user.
    /// Binary files are returned with empty <see cref="ProjectFileDto.Content"/>.
    /// </summary>
    public async Task<ProjectFileDto?> GetByPathAsync(Guid projectId, string path, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return null;

        var file = await fileRepo.GetByPathAsync(projectId, path);
        if (file is null) return null;

        var dto = MapMetadata(file);
        if (!file.IsBinary)
            dto.Content = await ReadContentAsync(file) ?? string.Empty;

        return dto;
    }

    /// <summary>
    /// Creates or replaces the text file at the given path: content is written to object
    /// storage first, then metadata is upserted. Returns <c>null</c> if the project does
    /// not exist or belongs to another user.
    /// </summary>
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
            StorageProvider = storage.Provider,
            StorageKey = existing?.StorageKey ?? $"{projectId}/{Guid.NewGuid():N}",
            IsBinary = false,
            CreatedAt = existing?.CreatedAt ?? DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var bytes = Encoding.UTF8.GetBytes(dto.Content);
        await using (var stream = new MemoryStream(bytes))
        {
            await storage.PutAsync(file.StorageKey, stream, "text/plain; charset=utf-8");
        }

        await fileRepo.UpsertAsync(file);

        var result = MapMetadata(file);
        result.Content = dto.Content;
        return result;
    }

    /// <summary>
    /// Deletes the file at the given path from both object storage and metadata.
    /// Returns <c>false</c> if the project or file does not exist or belongs to another user.
    /// </summary>
    public async Task<bool> DeleteAsync(Guid projectId, string path, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return false;

        var file = await fileRepo.GetByPathAsync(projectId, path);
        if (file is null) return false;

        await storage.DeleteAsync(file.StorageKey);
        await fileRepo.RemoveAsync(projectId, path);
        return true;
    }

    private async Task<string?> ReadContentAsync(ProjectFile file)
    {
        await using var stream = await storage.GetAsync(file.StorageKey);
        if (stream is null) return null;

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }

    private static ProjectFileDto MapMetadata(ProjectFile file) => new()
    {
        Id = file.Id,
        ProjectId = file.ProjectId,
        Path = file.Path,
        IsBinary = file.IsBinary,
        CreatedAt = file.CreatedAt,
        UpdatedAt = file.UpdatedAt
    };
}
