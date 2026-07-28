using LatexEditor.Infrastructure.Compile;

namespace LatexEditor.Infrastructure.Tests;

public class ChannelCompileQueueTests
{
    [Fact]
    public async Task Enqueue_ThenDequeue_ReturnsSameJobId()
    {
        var queue = new ChannelCompileQueue();
        var jobId = Guid.NewGuid();

        await queue.EnqueueAsync(jobId);
        var dequeued = await queue.DequeueAsync();

        Assert.Equal(jobId, dequeued);
    }

    [Fact]
    public async Task Dequeue_ReturnsJobsInFifoOrder()
    {
        var queue = new ChannelCompileQueue();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        await queue.EnqueueAsync(first);
        await queue.EnqueueAsync(second);

        Assert.Equal(first, await queue.DequeueAsync());
        Assert.Equal(second, await queue.DequeueAsync());
    }

    [Fact]
    public async Task Dequeue_WaitsForItem()
    {
        var queue = new ChannelCompileQueue();
        var jobId = Guid.NewGuid();

        var dequeueTask = queue.DequeueAsync().AsTask();
        Assert.False(dequeueTask.IsCompleted);

        await queue.EnqueueAsync(jobId);
        Assert.Equal(jobId, await dequeueTask.WaitAsync(TimeSpan.FromSeconds(5)));
    }
}
