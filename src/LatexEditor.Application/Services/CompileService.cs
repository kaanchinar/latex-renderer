using LatexEditor.Application.DTOs;
using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Application.Services;

/// <summary>
/// Use-case orchestration for compile jobs: triggering compiles, listing job
/// history, and resolving download URLs for produced PDFs. All operations are
/// scoped to the authenticated owner.
/// </summary>
public class CompileService(
    IProjectRepository projectRepo,
    ICompileJobRepository jobRepo,
    ICompileQueue queue,
    IFileStorage storage)
{
    private static readonly TimeSpan PdfUrlExpiry = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Creates a compile job for the project and enqueues it for background
    /// processing. Returns <c>null</c> if the project does not exist or belongs
    /// to another user.
    /// </summary>
    public async Task<CompileJobDto?> TriggerCompileAsync(Guid projectId, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return null;

        var job = new CompileJob
        {
            Id = Guid.NewGuid(),
            ProjectId = projectId,
            Status = CompileStatus.Queued
        };

        await jobRepo.AddAsync(job);
        await queue.EnqueueAsync(job.Id);

        return Map(job);
    }

    /// <summary>
    /// Returns the project's compile jobs, newest first. Returns an empty list
    /// if the project does not exist or belongs to another user.
    /// </summary>
    public async Task<IReadOnlyList<CompileJobDto>> GetJobsAsync(Guid projectId, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return [];

        var jobs = await jobRepo.GetByProjectIdAsync(projectId);
        return [.. jobs.Select(Map)];
    }

    /// <summary>
    /// Returns a short-lived download URL for the PDF produced by the given job.
    /// Returns <c>null</c> if the project or job does not exist, belongs to
    /// another user, or the job has no output.
    /// </summary>
    public async Task<string?> GetJobPdfUrlAsync(Guid projectId, Guid jobId, string ownerId)
    {
        var project = await projectRepo.GetByIdAsync(projectId, ownerId);
        if (project is null) return null;

        var job = await jobRepo.GetByIdAsync(jobId);
        if (job is null || job.ProjectId != projectId || job.OutputStorageKey is null) return null;

        return await storage.GetPresignedUrlAsync(job.OutputStorageKey, PdfUrlExpiry);
    }

    private static CompileJobDto Map(CompileJob job) => new()
    {
        Id = job.Id,
        ProjectId = job.ProjectId,
        Status = job.Status.ToString(),
        CreatedAt = job.CreatedAt,
        StartedAt = job.StartedAt,
        CompletedAt = job.CompletedAt,
        DurationMs = job.DurationMs,
        ErrorMessage = job.ErrorMessage,
        HasOutput = job.OutputStorageKey is not null
    };
}
