using LatexEditor.Core.Entities;

namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Publishes compile job lifecycle events to interested parties (e.g. SignalR clients).
/// Infrastructure implementations must be safe to call from a singleton worker.
/// </summary>
public interface ICompileEventPublisher
{
    /// <summary>The job has started running.</summary>
    Task PublishStartedAsync(CompileJob job, CancellationToken ct = default);

    /// <summary>The job succeeded and its PDF is available at the given URL.</summary>
    Task PublishCompletedAsync(CompileJob job, string pdfUrl, CancellationToken ct = default);

    /// <summary>The job failed or was cancelled, with a human-readable reason.</summary>
    Task PublishFailedAsync(CompileJob job, string error, CancellationToken ct = default);

    /// <summary>A line of compiler output is available (sent after the process exits).</summary>
    /// <remarks>Lines may come from standard output or standard error.</remarks>
    Task PublishOutputAsync(CompileJob job, string line, CancellationToken ct = default);
}
