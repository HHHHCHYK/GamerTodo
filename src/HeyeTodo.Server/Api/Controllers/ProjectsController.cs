using System.Security.Claims;
using HeyeTodo.Server.Application.Projects;
using HeyeTodo.Shared.Contracts.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeyeTodo.Server.Api.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public sealed class ProjectsController : ControllerBase
{
    private readonly IProjectService _projects;

    public ProjectsController(IProjectService projects)
    {
        _projects = projects;
    }

    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _projects.ListAsync(userId.Value, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectRequest request, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _projects.CreateAsync(userId.Value, NormalizeClientId(clientId), request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPatch("{projectId:guid}")]
    public async Task<IActionResult> Update(Guid projectId, [FromBody] UpdateProjectRequest request, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var normalized = request with { Id = projectId };
        var result = await _projects.UpdateAsync(userId.Value, NormalizeClientId(clientId), projectId, normalized, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpDelete("{projectId:guid}")]
    public async Task<IActionResult> Delete(Guid projectId, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _projects.DeleteAsync(userId.Value, NormalizeClientId(clientId), projectId, ct);
        return result.IsSuccess ? NoContent() : NotFound(new { error = result.Error });
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var g) ? g : null;
    }

    private static Guid NormalizeClientId(Guid clientId)
        => clientId == Guid.Empty ? Guid.NewGuid() : clientId;
}
