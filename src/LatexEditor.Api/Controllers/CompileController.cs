using LatexEditor.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using System.Security.Claims;

namespace LatexEditor.Api.Controllers;

/// <summary>
/// Compile jobs for a project owned by the authenticated user.
/// Compiles run in the background; poll the jobs list or use the SignalR hub
/// (<c>/hubs/projects</c>) for real-time completion events.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}")]
public class CompileController(CompileService service) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>Trigger a compile</summary>
    /// <remarks>
    /// Creates a compile job and queues it for background processing. Returns 404 if the
    /// project does not exist or belongs to another user. Rate-limited per user;
    /// returns 429 when the limit is exceeded.
    /// </remarks>
    [HttpPost("compile")]
    [EnableRateLimiting("compile")]
    public async Task<IActionResult> TriggerCompile(Guid projectId)
    {
        var job = await service.TriggerCompileAsync(projectId, CurrentUserId);
        if (job is null) return NotFound();
        return Ok(job);
    }

    /// <summary>List compile jobs</summary>
    /// <remarks>Returns the project's compile jobs, newest first.</remarks>
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs(Guid projectId)
    {
        var jobs = await service.GetJobsAsync(projectId, CurrentUserId);
        return Ok(jobs);
    }

    /// <summary>Get a job's PDF</summary>
    /// <remarks>
    /// Redirects to a short-lived download URL for the compiled PDF.
    /// Returns 404 if the job does not exist, belongs to another user, or has no output yet.
    /// </remarks>
    [HttpGet("jobs/{jobId:guid}/pdf")]
    public async Task<IActionResult> GetJobPdf(Guid projectId, Guid jobId)
    {
        var url = await service.GetJobPdfUrlAsync(projectId, jobId, CurrentUserId);
        if (url is null) return NotFound();
        return Redirect(url);
    }
}
