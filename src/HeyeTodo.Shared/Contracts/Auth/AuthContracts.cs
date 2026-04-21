using System;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Shared.Contracts.Auth;

public sealed record RegisterRequest(
    string Email,
    string Password,
    string DisplayName,
    RoleType Roles = RoleType.None);

public sealed record LoginRequest(
    string Email,
    string Password,
    Guid ClientId);

public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    UserDto User);

public sealed record UserDto(
    Guid Id,
    string Email,
    string DisplayName,
    RoleType Roles,
    RoleType? ActiveRoleContext);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UpdateRolesRequest(RoleType Roles, RoleType? ActiveRoleContext);
