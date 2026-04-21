using HeyeTodo.Server.Application.Common;
using HeyeTodo.Server.Domain.Entities;
using HeyeTodo.Server.Infrastructure.Auth;
using HeyeTodo.Server.Infrastructure.Localization;
using HeyeTodo.Server.Infrastructure.Persistence;
using HeyeTodo.Shared.Contracts.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace HeyeTodo.Server.Application.Auth;

public interface IAuthService
{
    Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest req, Guid clientId, CancellationToken ct);
    Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct);
    Task<ServiceResult<AuthResponse>> RefreshAsync(RefreshRequest req, CancellationToken ct);
    Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken ct);
}

public sealed class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly ITokenService _tokens;
    private readonly JwtOptions _jwt;
    private readonly IStringLocalizer<SharedResource> _loc;

    public AuthService(
        AppDbContext db,
        ITokenService tokens,
        IOptions<JwtOptions> jwt,
        IStringLocalizer<SharedResource> loc)
    {
        _db = db;
        _tokens = tokens;
        _jwt = jwt.Value;
        _loc = loc;
    }

    public async Task<ServiceResult<AuthResponse>> RegisterAsync(RegisterRequest req, Guid clientId, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return ServiceResult<AuthResponse>.Fail(_loc["Auth_EmailAndPasswordRequired"].Value);

        var normalized = req.Email.Trim().ToLowerInvariant();
        if (await _db.Users.AnyAsync(u => u.Email == normalized, ct))
            return ServiceResult<AuthResponse>.Fail(_loc["Auth_EmailAlreadyRegistered"].Value);

        var user = new AppUser
        {
            Email = normalized,
            DisplayName = string.IsNullOrWhiteSpace(req.DisplayName) ? normalized : req.DisplayName.Trim(),
            PasswordHash = PasswordHasher.Hash(req.Password),
            Roles = req.Roles,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await IssueAsync(user, clientId, ct);
    }

    public async Task<ServiceResult<AuthResponse>> LoginAsync(LoginRequest req, CancellationToken ct)
    {
        var normalized = req.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Email == normalized, ct);
        if (user is null || !PasswordHasher.Verify(req.Password, user.PasswordHash))
            return ServiceResult<AuthResponse>.Fail(_loc["Auth_InvalidCredentials"].Value);

        return await IssueAsync(user, req.ClientId, ct);
    }

    public async Task<ServiceResult<AuthResponse>> RefreshAsync(RefreshRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.RefreshToken))
            return ServiceResult<AuthResponse>.Fail(_loc["Auth_RefreshInvalid"].Value);

        var hash = _tokens.HashRefreshSecret(req.RefreshToken);
        var existing = await _db.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (existing is null || existing.RevokedAt != null || existing.ExpiresAt <= DateTimeOffset.UtcNow)
            return ServiceResult<AuthResponse>.Fail(_loc["Auth_RefreshInvalid"].Value);

        existing.RevokedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        return await IssueAsync(existing.User!, existing.ClientId, ct);
    }

    public async Task<ServiceResult> LogoutAsync(string refreshToken, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(refreshToken)) return ServiceResult.Ok();

        var hash = _tokens.HashRefreshSecret(refreshToken);
        var existing = await _db.RefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (existing is { RevokedAt: null })
        {
            existing.RevokedAt = DateTimeOffset.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult<AuthResponse>> IssueAsync(AppUser user, Guid clientId, CancellationToken ct)
    {
        var (access, expiresAt) = _tokens.IssueAccessToken(user);

        var refreshSecret = _tokens.NewRefreshTokenSecret();
        var refreshHash = _tokens.HashRefreshSecret(refreshSecret);

        _db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id,
            ClientId = clientId,
            TokenHash = refreshHash,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(_jwt.RefreshTokenDays),
        });
        await _db.SaveChangesAsync(ct);

        var dto = new UserDto(user.Id, user.Email, user.DisplayName, user.Roles, user.ActiveRoleContext);
        return ServiceResult<AuthResponse>.Ok(new AuthResponse(access, refreshSecret, expiresAt, dto));
    }
}
