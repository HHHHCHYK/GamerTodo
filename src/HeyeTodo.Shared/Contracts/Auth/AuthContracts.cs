using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Shared.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string? DisplayName,
    RoleType Roles = RoleType.None);

public sealed record LoginRequest(
    string Email,
    string Password,
    Guid ClientId);

public sealed record RefreshRequest(string RefreshToken);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    RoleType Roles,
    RoleType? ActiveRoleContext);

public sealed record UpdateRolesRequest(
    RoleType Roles,
    RoleType? ActiveRoleContext);
