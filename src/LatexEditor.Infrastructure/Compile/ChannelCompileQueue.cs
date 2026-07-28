using System.Threading.Channels;
using LatexEditor.Core.Interfaces;

namespace LatexEditor.Infrastructure.Compile;

/// <summary>
/// In-memory <see cref="ICompileQueue"/> backed by an unbounded channel.
/// Register as a singleton so the worker and producers share the same queue.
/// </summary>
public class ChannelCompileQueue : ICompileQueue
{
    private readonly Channel<Guid> _channel = Channel.CreateUnbounded<Guid>();

    /// <inheritdoc />
    public ValueTask EnqueueAsync(Guid jobId, CancellationToken ct = default)
    {
        return _channel.Writer.WriteAsync(jobId, ct);
    }

    /// <inheritdoc />
    public async ValueTask<Guid> DequeueAsync(CancellationToken ct = default)
    {
        return await _channel.Reader.ReadAsync(ct);
    }
}
