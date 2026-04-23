using System.Security.Claims;
using HeyeTodo.Server.Application.Tasks;
using HeyeTodo.Shared.Contracts.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeyeTodo.Server.Api.Controllers;

[ApiController]
[Route("api/tasks")]
[Authorize]
public sealed class TasksController : ControllerBase
{
    private readonly ITaskService _tasks;

    public TasksController(ITaskService tasks)
    {
        _tasks = tasks;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] TaskListQuery query, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.ListAsync(userId.Value, query, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpGet("{taskId:guid}")]
    public async Task<IActionResult> Get(Guid taskId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.GetAsync(userId.Value, taskId, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateTaskRequest request, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.CreateAsync(userId.Value, NormalizeClientId(clientId), request, ct);
        return result.IsSuccess ? Ok(result.Value) : BadRequest(new { error = result.Error });
    }

    [HttpPatch("{taskId:guid}")]
    public async Task<IActionResult> Update(Guid taskId, [FromBody] UpdateTaskRequest request, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var normalized = request with { Id = taskId };
        var result = await _tasks.UpdateAsync(userId.Value, NormalizeClientId(clientId), taskId, normalized, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpPatch("{taskId:guid}/status")]
    public async Task<IActionResult> ChangeStatus(Guid taskId, [FromBody] ChangeTaskStatusRequest request, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var normalized = request with { Id = taskId };
        var result = await _tasks.ChangeStatusAsync(userId.Value, NormalizeClientId(clientId), taskId, normalized, ct);
        return result.IsSuccess ? Ok(result.Value) : NotFound(new { error = result.Error });
    }

    [HttpDelete("{taskId:guid}")]
    public async Task<IActionResult> Delete(Guid taskId, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _tasks.DeleteAsync(userId.Value, NormalizeClientId(clientId), taskId, ct);
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
