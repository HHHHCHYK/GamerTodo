using System;

namespace HeyeTodo.Shared.Enums;

/// <summary>
/// User-selectable role(s) within the team context.
/// A user can have zero or more roles combined via bitwise OR.
/// </summary>
[Flags]
public enum RoleType
{
    None           = 0,
    Producer       = 1 << 0,
    Designer       = 1 << 1,
    Artist         = 1 << 2,
    Programmer     = 1 << 3,
    SoundDesigner  = 1 << 4,
}
