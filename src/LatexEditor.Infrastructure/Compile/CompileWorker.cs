using LatexEditor.Core.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LatexEditor.Infrastructure.Compile;

/// <summary>
/// Hosted background worker that dequeues compile jobs from <see cref="ICompileQueue"/>
/// and processes them sequentially. A new DI scope is created per job so scoped
/// repositories resolve correctly.
/// </summary>
public class CompileWorker(
    ICompileQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<CompileWorker> logger) : BackgroundService
{
    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid jobId;
            try
            {
                jobId = await queue.DequeueAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<CompileJobProcessor>();
                await processor.ProcessAsync(jobId, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception e)
            {
                logger.LogError(e, "Unhandled error while processing compile job {JobId}", jobId);
            }
        }
    }
}
