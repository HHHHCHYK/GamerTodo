using System.Security.Claims;
using HeyeTodo.Server.Application.Sync;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using HeyeTodo.Shared.Contracts.Sync;

namespace HeyeTodo.Server.Api.Controllers;

[ApiController]
[Route("api/sync")]
[Authorize]
public sealed class SyncController : ControllerBase
{
    private readonly ISyncService _sync;

    public SyncController(ISyncService sync)
    {
        _sync = sync;
    }

    [HttpPost("push")]
    public async Task<IActionResult> Push([FromBody] SyncPushRequest request, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _sync.PushAsync(userId.Value, NormalizeClientId(clientId == Guid.Empty ? request.ClientId : clientId), request, ct);
        return Ok(response);
    }

    [HttpGet("pull")]
    public async Task<IActionResult> Pull([FromQuery] long sinceServerVersion, CancellationToken ct)
    {
        var userId = GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        var response = await _sync.PullAsync(userId.Value, sinceServerVersion, ct);
        return Ok(response);
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
