namespace HeyeTodo.Client.ViewModels;

/// <summary>
/// Placeholder for M2 (Task CRUD + List view).
/// </summary>
public sealed class TaskListViewModel : ViewModelBase
{
}

/// <summary>
/// Placeholder for M4 (Gantt chart canvas).
/// </summary>
public sealed class GanttViewModel : ViewModelBase
{
}

/// <summary>Coming-soon placeholder (M7).</summary>
public sealed class MiniGamesHubViewModel : ViewModelBase
{
}

/// <summary>Settings view model (language, server URL, roles, planning mode). Wired up in M1.</summary>
public sealed class SettingsViewModel : ViewModelBase
{
}

public sealed partial class SplashViewModel : ViewModelBase
{
    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string _status = string.Empty;

    [CommunityToolkit.Mvvm.ComponentModel.ObservableProperty]
    private string? _errorMessage;
}
