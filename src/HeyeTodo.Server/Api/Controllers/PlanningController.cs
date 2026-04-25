using System.Security.Claims;
using HeyeTodo.Server.Application.Planning;
using HeyeTodo.Shared.Contracts.Planning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HeyeTodo.Server.Api.Controllers;

[ApiController]
[Route("api/planning")]
[Authorize]
public sealed class PlanningController : ControllerBase
{
    private readonly IPlanningService _planning;

    public PlanningController(IPlanningService planning)
    {
        _planning = planning;
    }

    [HttpPost("plan")]
    public async Task<IActionResult> Plan([FromBody] PlanningRequest request, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null) return Unauthorized();

        var result = await _planning.PlanAsync(userId.Value, request, ct);
        return Ok(result);
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var g) ? g : null;
    }
}
