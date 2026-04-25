namespace HeyeTodo.Shared.Enums;

/// <summary>
/// Gantt dependency relationship type.
/// FS = Finish-to-Start, SS = Start-to-Start, FF = Finish-to-Finish, SF = Start-to-Finish.
/// </summary>
public enum DependencyType
{
    FinishToStart = 0,
    StartToStart = 1,
    FinishToFinish = 2,
    StartToFinish = 3,
}
