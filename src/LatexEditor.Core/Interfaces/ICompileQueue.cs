namespace LatexEditor.Core.Interfaces;

/// <summary>
/// Queue of pending compile job IDs. v1 is an in-memory channel; swappable for
/// RabbitMQ/Redis later without changing producers or the worker.
/// </summary>
public interface ICompileQueue
{
    /// <summary>Enqueues a compile job ID for processing.</summary>
    ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default);

    /// <summary>Waits for and returns the next job ID. Completes when cancelled.</summary>
    ValueTask<Guid> DequeueAsync(CancellationToken ct = default);
}
