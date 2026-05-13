namespace HeyeTodo.Shared.Enums;

[Flags]
public enum RoleType
{
    None = 0,
    Programmer = 1 << 0,
    Artist = 1 << 1,
    Designer = 1 << 2,
    Producer = 1 << 3,
    Audio = 1 << 4,
    Writer = 1 << 5,
    Tester = 1 << 6,
}
