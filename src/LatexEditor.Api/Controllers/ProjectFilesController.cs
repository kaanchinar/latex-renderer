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

    /// <summary>Lists file metadata for the project. Content is not included.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId)
    {
        var files = await service.GetByProjectIdAsync(projectId, CurrentUserId);
        return Ok(files);
    }

    /// <summary>Returns a single file including its text content, or 404.</summary>
    [HttpGet("{path}")]
    public async Task<IActionResult> GetByPath(Guid projectId, string path)
    {
        var file = await service.GetByPathAsync(projectId, path, CurrentUserId);
        if (file is null) return NotFound();
        return Ok(file);
    }

    /// <summary>Creates or replaces the text file at the given path, or returns 404 if the project does not exist.</summary>
    [HttpPut("{path}")]
    public async Task<IActionResult> Upsert(Guid projectId, string path, UpsertFileDto dto)
    {
        var file = await service.UpsertAsync(projectId, path, dto, CurrentUserId);
        if (file is null) return NotFound();
        return Ok(file);
    }

    /// <summary>Deletes the file at the given path, or returns 404.</summary>
    [HttpDelete("{path}")]
    public async Task<IActionResult> Delete(Guid projectId, string path)
    {
        var deleted = await service.DeleteAsync(projectId, path, CurrentUserId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
