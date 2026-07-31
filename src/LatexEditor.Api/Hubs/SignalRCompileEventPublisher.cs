using LatexEditor.Core.Entities;
using LatexEditor.Core.Interfaces;
using Microsoft.AspNetCore.SignalR;

namespace LatexEditor.Api.Hubs;

/// <summary>
/// <see cref="ICompileEventPublisher"/> that broadcasts job lifecycle events to
/// the project's SignalR group. Singleton-safe: <see cref="IHubContext{THub,TClient}"/>
/// can be injected into singletons.
/// </summary>
public class SignalRCompileEventPublisher(IHubContext<ProjectHub, IProjectClient> hubContext) : ICompileEventPublisher
{
    /// <inheritdoc />
    public Task PublishStartedAsync(CompileJob job, CancellationToken ct = default)
    {
        return Group(job).CompileStarted(job.Id);
    }

    /// <inheritdoc />
    public Task PublishCompletedAsync(CompileJob job, string pdfUrl, CancellationToken ct = default)
    {
        return Group(job).CompileCompleted(job.Id, pdfUrl);
    }

    /// <inheritdoc />
    public Task PublishFailedAsync(CompileJob job, string error, CancellationToken ct = default)
    {
        return Group(job).CompileFailed(job.Id, error);
    }

    /// <inheritdoc />
    public Task PublishOutputAsync(CompileJob job, string line, CancellationToken ct = default)
    {
        return Group(job).CompileOutput(line);
    }

    private IProjectClient Group(CompileJob job) =>
        hubContext.Clients.Group(ProjectHub.GroupName(job.ProjectId));
}
