using System.Security.Claims;
using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using LatexEditor.Infrastructure.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace LatexEditor.Api.Hubs;

/// <summary>
/// Real-time project channel. Clients join a project's group to receive compile
/// lifecycle events (<see cref="IProjectClient"/>) and can trigger compiles or
/// update files without HTTP round trips. All operations are owner-scoped.
/// </summary>
[Authorize]
public class ProjectHub(
    ProjectService projectService,
    ProjectFileService fileService,
    CompileService compileService) : Hub<IProjectClient>
{
    /// <summary>SignalR group name for a project.</summary>
    public static string GroupName(Guid projectId) => $"project-{projectId}";

    private string CurrentUserId =>
        Context.User?.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new HubException("Unauthenticated.");

    /// <inheritdoc />
    public override Task OnConnectedAsync()
    {
        CompileTelemetry.ChangeHubConnections(1);
        return base.OnConnectedAsync();
    }

    /// <inheritdoc />
    public override Task OnDisconnectedAsync(Exception? exception)
    {
        CompileTelemetry.ChangeHubConnections(-1);
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribes the connection to a project's compile events.
    /// Throws a <see cref="HubException"/> if the project does not exist or belongs to another user.
    /// </summary>
    public async Task JoinProject(Guid projectId)
    {
        var project = await projectService.GetByIdAsync(projectId, CurrentUserId);
        if (project is null) throw new HubException("Project not found.");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
    }

    /// <summary>
    /// Creates and queues a compile job for the project. Returns the job ID.
    /// </summary>
    public async Task<Guid> TriggerCompile(Guid projectId)
    {
        var job = await compileService.TriggerCompileAsync(projectId, CurrentUserId);
        if (job is null) throw new HubException("Project not found.");

        return job.Id;
    }

    /// <summary>
    /// Creates or replaces a text file in the project.
    /// </summary>
    public async Task UpdateFile(Guid projectId, string path, string content)
    {
        var result = await fileService.UpsertAsync(
            projectId, path, new UpsertFileDto { Content = content }, CurrentUserId);
        if (result is null) throw new HubException("Project not found.");
    }
}
