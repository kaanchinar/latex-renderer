using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LatexEditor.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/projects/{projectId:guid}/files")]
public class ProjectFilesController(ProjectFileService service) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet]
    public async Task<IActionResult> GetAll(Guid projectId)
    {
        var files = await service.GetByProjectIdAsync(projectId, CurrentUserId);
        return Ok(files);
    }

    [HttpGet("{path}")]
    public async Task<IActionResult> GetByPath(Guid projectId, string path)
    {
        var file = await service.GetByPathAsync(projectId, path, CurrentUserId);
        if (file is null) return NotFound();
        return Ok(file);
    }

    [HttpPut("{path}")]
    public async Task<IActionResult> Upsert(Guid projectId, string path, UpsertFileDto dto)
    {
        var file = await service.UpsertAsync(projectId, path, dto, CurrentUserId);
        if (file is null) return NotFound();
        return Ok(file);
    }

    [HttpDelete("{path}")]
    public async Task<IActionResult> Delete(Guid projectId, string path)
    {
        var deleted = await service.DeleteAsync(projectId, path, CurrentUserId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
