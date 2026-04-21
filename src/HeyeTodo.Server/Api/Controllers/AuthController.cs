using System.Security.Claims;
using HeyeTodo.Server.Application.Auth;
using HeyeTodo.Server.Infrastructure.Persistence;
using HeyeTodo.Shared.Contracts.Auth;
using HeyeTodo.Shared.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Server.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;
    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest req, [FromHeader(Name = "X-Client-Id")] Guid clientId, CancellationToken ct)
    {
        var r = await _auth.RegisterAsync(req, clientId == Guid.Empty ? Guid.NewGuid() : clientId, ct);
        return r.IsSuccess ? Ok(r.Value) : BadRequest(new { error = r.Error });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest req, CancellationToken ct)
    {
        var r = await _auth.LoginAsync(req, ct);
        return r.IsSuccess ? Ok(r.Value) : Unauthorized(new { error = r.Error });
    }

    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest req, CancellationToken ct)
    {
        var r = await _auth.RefreshAsync(req, ct);
        return r.IsSuccess ? Ok(r.Value) : Unauthorized(new { error = r.Error });
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest req, CancellationToken ct)
    {
        await _auth.LogoutAsync(req.RefreshToken, ct);
        return NoContent();
    }
}

[ApiController]
[Route("api/users")]
[Authorize]
public sealed class UsersController : ControllerBase
{
    private readonly AppDbContext _db;
    public UsersController(AppDbContext db) => _db = db;

    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return Unauthorized();
        var u = await _db.Users.FindAsync([uid.Value], ct);
        if (u is null) return NotFound();
        return Ok(new UserDto(u.Id, u.Email, u.DisplayName, u.Roles, u.ActiveRoleContext));
    }

    [HttpPatch("me/roles")]
    public async Task<IActionResult> UpdateRoles([FromBody] UpdateRolesRequest req, CancellationToken ct)
    {
        var uid = GetUserId();
        if (uid is null) return Unauthorized();
        var u = await _db.Users.FindAsync([uid.Value], ct);
        if (u is null) return NotFound();
        u.Roles = req.Roles;
        u.ActiveRoleContext = req.ActiveRoleContext;
        await _db.SaveChangesAsync(ct);
        return Ok(new UserDto(u.Id, u.Email, u.DisplayName, u.Roles, u.ActiveRoleContext));
    }

    private Guid? GetUserId()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");
        return Guid.TryParse(sub, out var g) ? g : null;
    }
}
