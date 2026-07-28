using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace LatexEditor.Api.Controllers;

/// <summary>
/// CRUD for projects owned by the authenticated user.
/// All endpoints require cookie authentication and are owner-scoped.
/// </summary>
[ApiController]
[Authorize]
[Route("api/projects")]
public class ProjectsController(ProjectService service) : ControllerBase
{
    private string CurrentUserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    /// <summary>List projects</summary>
    /// <remarks>Returns all projects owned by the current user.</remarks>
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await service.GetByOwnerAsync(CurrentUserId);
        return Ok(projects);
    }

    /// <summary>Create a project</summary>
    /// <remarks>The project is owned by the current user.</remarks>
    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectDto dto)
    {
        var project = await service.CreateAsync(dto, CurrentUserId);
        return Ok(project);
    }

    /// <summary>Get a project</summary>
    /// <remarks>Returns 404 if the project does not exist or belongs to another user.</remarks>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var project = await service.GetByIdAsync(id, CurrentUserId);
        if (project is null)
        {
            return NotFound();
        }
        return Ok(project);
    }

    /// <summary>Rename a project</summary>
    /// <remarks>Returns 404 if the project does not exist or belongs to another user.</remarks>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectDto dto)
    {
        var updated = await service.UpdateAsync(id, dto, CurrentUserId);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    /// <summary>Delete a project</summary>
    /// <remarks>Returns 404 if the project does not exist or belongs to another user.</remarks>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await service.DeleteAsync(id, CurrentUserId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
