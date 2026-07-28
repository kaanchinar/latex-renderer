using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LatexEditor.Api.Controllers;

/// <summary>
/// File operations within a project owned by the authenticated user.
/// File content is stored in object storage; these endpoints work with text content over JSON.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/files")]
public class ProjectFilesController(ProjectFileService service) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>List project files</summary>
    /// <remarks>Returns file metadata only; content is not included.</remarks>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId)
    {
        var files = await service.GetByProjectIdAsync(projectId, CurrentUserId);
        return Ok(files);
    }

    /// <summary>Get a file</summary>
    /// <remarks>Returns the file including its text content, or 404 if the project or file does not exist.</remarks>
    [HttpGet("{path}")]
    public async Task<IActionResult> GetByPath(Guid projectId, string path)
    {
        var file = await service.GetByPathAsync(projectId, path, CurrentUserId);
        if (file is null) return NotFound();
        return Ok(file);
    }

    /// <summary>Create or replace a file</summary>
    /// <remarks>Writes the text content at the given path. Returns 404 if the project does not exist.</remarks>
    [HttpPut("{path}")]
    public async Task<IActionResult> Upsert(Guid projectId, string path, UpsertFileDto dto)
    {
        var file = await service.UpsertAsync(projectId, path, dto, CurrentUserId);
        if (file is null) return NotFound();
        return Ok(file);
    }

    /// <summary>Delete a file</summary>
    /// <remarks>Deletes the file at the given path from storage and metadata. Returns 404 if it does not exist.</remarks>
    [HttpDelete("{path}")]
    public async Task<IActionResult> Delete(Guid projectId, string path)
    {
        var deleted = await service.DeleteAsync(projectId, path, CurrentUserId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
