using System;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Server.Domain.Entities;

public sealed class AppUser
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = null!;
    public string PasswordHash { get; set; } = null!;
    public string DisplayName { get; set; } = null!;

    /// <summary>Bitwise combination of RoleType.</summary>
    public RoleType Roles { get; set; } = RoleType.None;

    /// <summary>Active role viewport context; null when not chosen.</summary>
    public RoleType? ActiveRoleContext { get; set; }

    public string PreferredLanguage { get; set; } = "auto";

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
