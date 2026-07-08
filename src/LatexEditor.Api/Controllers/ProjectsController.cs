using LatexEditor.Application.DTOs;
using LatexEditor.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace LatexEditor.Api.Controllers;

[ApiController]
[Route("api/projects")]
public class ProjectsController(ProjectService service) : ControllerBase
{
    private string CurrentUserId => "demo-user";

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var projects = await service.GetByOwnerAsync(CurrentUserId);
        return Ok(projects);
    }

    [HttpPost]
    public async Task<IActionResult> Create(CreateProjectDto dto)
    {
        var project = await service.CreateAsync(dto, CurrentUserId);
        return Ok(project);
    }

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

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateProjectDto dto)
    {
        var updated = await service.UpdateAsync(id, dto, CurrentUserId);
        if (updated is null) return NotFound();
        return Ok(updated);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id)
    {
        var deleted = await service.DeleteAsync(id, CurrentUserId);
        if (!deleted) return NotFound();
        return NoContent();
    }
}
