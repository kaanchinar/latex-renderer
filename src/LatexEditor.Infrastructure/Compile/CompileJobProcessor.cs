using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using LatexEditor.Infrastructure.Telemetry;
using Microsoft.Extensions.Logging;

namespace LatexEditor.Infrastructure.Compile;

/// <summary>
/// Processes a single compile job end to end: downloads project files into an
/// isolated temp directory, runs Tectonic, verifies and uploads the resulting
/// PDF, and records the outcome on the job and project. The temp directory is
/// always removed, even on failure.
/// </summary>
public class CompileJobProcessor(
    IProjectRepository projectRepo,
    IProjectFileRepository fileRepo,
    ICompileJobRepository jobRepo,
    IFileStorage storage,
    ITectonicCompiler compiler,
    ICompileEventPublisher events,
    ILogger<CompileJobProcessor> logger)
{
    /// <summary>
    /// Preferred entry document name. When multiple <c>.tex</c> files contain
    /// <c>\documentclass</c>, this name is chosen; otherwise the first match is used.
    /// If no file contains <c>\documentclass</c>, this file is used as a fallback.
    /// </summary>
    public const string EntryFileName = "main.tex";

    private static readonly TimeSpan PdfUrlExpiry = TimeSpan.FromMinutes(15);

    /// <summary>Processes the job with the given ID. No-op if the job no longer exists.</summary>
    public async Task ProcessAsync(Guid jobId, CancellationToken ct = default)
    {
        using var activity = CompileTelemetry.ActivitySource.StartActivity("compile.process");
        activity?.SetTag("compile.job_id", jobId.ToString());

        var job = await jobRepo.GetByIdAsync(jobId);
        if (job is null)
        {
            logger.LogWarning("Compile job {JobId} not found; skipping", jobId);
            return;
        }
        activity?.SetTag("compile.project_id", job.ProjectId.ToString());

        job.Status = CompileStatus.Running;
        job.StartedAt = DateTime.UtcNow;
        await jobRepo.UpdateAsync(job);
        await SafePublishAsync(() => events.PublishStartedAsync(job, ct));

        var tempDir = Path.Combine(Path.GetTempPath(), $"latex-compile-{job.Id:N}");
        try
        {
            Directory.CreateDirectory(tempDir);
            await DownloadProjectFilesAsync(job.ProjectId, tempDir, ct);

            var entryFile = FindEntryFile(tempDir);
            if (entryFile is null)
            {
                await FailAsync(job, "No LaTeX entry file found (no .tex file with \\documentclass).", "", "");
                return;
            }

            var result = await compiler.CompileAsync(tempDir, entryFile, ct);

            job.StdOut = result.StdOut;
            job.StdErr = result.StdErr;
            await PublishOutputLinesAsync(job, result.StdOut, ct);

            if (result.TimedOut)
            {
                await FailAsync(job, "Compile timed out.", result.StdOut, result.StdErr);
                return;
            }

            if (result.OutputPdfPath is null)
            {
                await FailAsync(job, $"Tectonic exited with code {result.ExitCode}.", result.StdOut, result.StdErr);
                return;
            }

            if (!await IsPdfAsync(result.OutputPdfPath, ct))
            {
                await FailAsync(job, "Compiler output failed PDF verification.", result.StdOut, result.StdErr);
                return;
            }

            var outputKey = $"{job.ProjectId}/jobs/{job.Id}/output.pdf";
            await using (var pdfStream = File.OpenRead(result.OutputPdfPath))
            {
                await storage.PutAsync(outputKey, pdfStream, "application/pdf", ct);
            }

            job.Status = CompileStatus.Success;
            job.OutputStorageKey = outputKey;
            await CompleteAsync(job);

            var pdfUrl = await storage.GetPresignedUrlAsync(outputKey, PdfUrlExpiry, ct);
            await SafePublishAsync(() => events.PublishCompletedAsync(job, pdfUrl, ct));
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            job.Status = CompileStatus.Cancelled;
            job.ErrorMessage = "Compile was cancelled.";
            await CompleteAsync(job, saveOutput: false);
            await SafePublishAsync(() => events.PublishFailedAsync(job, job.ErrorMessage));
            throw;
        }
        catch (Exception e)
        {
            logger.LogError(e, "Compile job {JobId} failed with an unexpected error", jobId);
            await FailAsync(job, $"Unexpected error: {e.Message}", job.StdOut, job.StdErr);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to clean up compile temp directory {TempDir}", tempDir);
            }
        }
    }

    private async Task DownloadProjectFilesAsync(Guid projectId, string tempDir, CancellationToken ct)
    {
        var files = await fileRepo.GetByProjectIdAsync(projectId);
        foreach (var file in files)
        {
            var targetPath = Path.GetFullPath(Path.Combine(tempDir, file.Path));
            if (!targetPath.StartsWith(Path.GetFullPath(tempDir) + Path.DirectorySeparatorChar))
            {
                logger.LogWarning("Skipping file with unsafe path {Path} in project {ProjectId}", file.Path, projectId);
                continue;
            }

            await using var source = await storage.GetAsync(file.StorageKey, ct);
            if (source is null)
            {
                logger.LogWarning("Storage object {StorageKey} missing for file {Path}", file.StorageKey, file.Path);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            await using var target = new FileStream(targetPath, FileMode.Create, FileAccess.Write);
            await source.CopyToAsync(target, ct);
        }
    }

    /// <summary>
    /// Returns the project entry file relative to <paramref name="tempDir"/>.
    /// Picks the first <c>.tex</c> file containing <c>\documentclass</c>,
    /// preferring <see cref="EntryFileName"/> when several match. Falls back to
    /// <see cref="EntryFileName"/> if it exists on disk even without a
    /// <c>\documentclass</c> declaration. Returns <c>null</c> when no entry file
    /// can be determined.
    /// </summary>
    private static string? FindEntryFile(string tempDir)
    {
        string? firstMatch = null;
        foreach (var file in Directory.EnumerateFiles(tempDir, "*.tex", SearchOption.AllDirectories))
        {
            var content = File.ReadAllText(file);
            if (!content.Contains("\\documentclass")) continue;

            var relativePath = Path.GetRelativePath(tempDir, file);
            if (string.Equals(relativePath, EntryFileName, StringComparison.OrdinalIgnoreCase))
                return relativePath;

            firstMatch ??= relativePath;
        }

        if (firstMatch is not null)
            return firstMatch;

        var mainTexPath = Path.Combine(tempDir, EntryFileName);
        return File.Exists(mainTexPath) ? EntryFileName : null;
    }

    private static async Task<bool> IsPdfAsync(string path, CancellationToken ct)
    {
        var header = new byte[4];
        await using var stream = File.OpenRead(path);
        var read = await stream.ReadAsync(header, ct);
        return read == 4 && header[0] == (byte)'%' && header[1] == (byte)'P' &&
               header[2] == (byte)'D' && header[3] == (byte)'F';
    }

    private async Task FailAsync(CompileJob job, string error, string stdOut, string stdErr)
    {
        job.Status = CompileStatus.Failed;
        job.ErrorMessage = error;
        job.StdOut = stdOut;
        job.StdErr = stdErr;
        await CompleteAsync(job, saveOutput: false);
        await SafePublishAsync(() => events.PublishFailedAsync(job, error));
    }

    private async Task PublishOutputLinesAsync(CompileJob job, string stdOut, CancellationToken ct)
    {
        foreach (var line in stdOut.Split('\n', StringSplitOptions.RemoveEmptyEntries))
        {
            await SafePublishAsync(() => events.PublishOutputAsync(job, line.TrimEnd('\r'), ct));
        }
    }

    private async Task SafePublishAsync(Func<Task> publish)
    {
        try
        {
            await publish();
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Failed to publish compile event");
        }
    }

    private async Task CompleteAsync(CompileJob job, bool saveOutput = true)
    {
        job.CompletedAt = DateTime.UtcNow;
        job.DurationMs = job.StartedAt.HasValue
            ? (long)(job.CompletedAt.Value - job.StartedAt.Value).TotalMilliseconds
            : null;
        if (!saveOutput) job.OutputStorageKey = null;

        var outcome = job.Status.ToString().ToLowerInvariant();
        System.Diagnostics.Activity.Current?.SetTag("compile.outcome", outcome);
        CompileTelemetry.RecordJobCompleted(outcome, (job.DurationMs ?? 0) / 1000.0);

        await jobRepo.UpdateAsync(job);

        var project = await projectRepo.GetByIdUnrestrictedAsync(job.ProjectId);
        if (project is not null)
        {
            project.LastCompileStatus = job.Status;
            await projectRepo.UpdateAsync(project);
        }
    }
}
