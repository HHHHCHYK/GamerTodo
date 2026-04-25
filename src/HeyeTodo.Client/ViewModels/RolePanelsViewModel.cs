using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Application.Tasks;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using HeyeTodo.Shared.RolePanels;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class RolePanelsViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceService _workspace;
    private readonly ISyncCoordinator _sync;
    private readonly ClientSession _session;
    private readonly Guid _clientId;
    private bool _reloadingSelection;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<RolePanelTabViewModel> RolePanels { get; } = new();
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();
    public ObservableCollection<RoleDashboardCardViewModel> DashboardCards { get; } = new();
    public ObservableCollection<RoleTaskActionViewModel> Actions { get; } = new();
    public ObservableCollection<RoleFieldEditorViewModel> FieldEditors { get; } = new();

    [ObservableProperty] private ProjectItemViewModel? _selectedProject;
    [ObservableProperty] private RolePanelTabViewModel? _selectedRolePanel;
    [ObservableProperty] private TaskItemViewModel? _selectedTask;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;

    public bool HasProjects => Projects.Count > 0;
    public bool HasRoles => RolePanels.Count > 0;
    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasTasks => Tasks.Count > 0;
    public bool HasSelectedTask => SelectedTask is not null;
    public bool ShowNoRolesMessage => !HasRoles;
    public bool ShowNoProjectMessage => HasRoles && !HasSelectedProject;
    public bool ShowEmptyTasksMessage => HasRoles && HasSelectedProject && !HasTasks;
    public bool ShowTaskList => HasRoles && HasSelectedProject && HasTasks;
    public bool ShowRoleEditor => HasRoles && HasSelectedProject && HasSelectedTask;

    public RolePanelsViewModel(ITaskWorkspaceService workspace, ISyncCoordinator sync)
    {
        _workspace = workspace;
        _sync = sync;
        _session = AppHost.Services.GetRequiredService<ClientSession>();
        _clientId = AppPaths.GetOrCreateClientId();
        _sync.ProjectInvalidated += OnProjectInvalidated;
        BuildRolePanels();
        _ = LoadAsync();
    }

    partial void OnSelectedProjectChanged(ProjectItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(ShowNoProjectMessage));
        OnPropertyChanged(nameof(ShowEmptyTasksMessage));
        OnPropertyChanged(nameof(ShowTaskList));
        OnPropertyChanged(nameof(ShowRoleEditor));

        if (_reloadingSelection)
        {
            return;
        }

        SelectedTask = null;
        if (value is null)
        {
            Tasks.Clear();
            OnPropertyChanged(nameof(HasTasks));
            return;
        }

        _ = _sync.SubscribeProjectAsync(value.Id);
        _ = LoadTasksAsync();
    }

    partial void OnSelectedRolePanelChanged(RolePanelTabViewModel? value)
    {
        RefreshRoleContent();
        if (!_reloadingSelection)
        {
            _ = LoadTasksAsync();
        }
    }

    partial void OnSelectedTaskChanged(TaskItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedTask));
        OnPropertyChanged(nameof(ShowRoleEditor));
        BuildFieldEditors();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    [RelayCommand]
    private async Task ApplyActionAsync(RoleTaskActionViewModel? action)
    {
        if (action is null || SelectedTask is null || _session.UserId is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            var updated = await _workspace.ChangeTaskStatusAsync(
                _session.UserId.Value,
                SelectedTask.Id,
                new ChangeTaskStatusRequest(SelectedTask.Id, action.TargetStatus),
                _clientId);
            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.StatusUpdateFailed"];
                return;
            }

            await LoadTasksAsync(updated.Value.Id);
            StatusMessage = updated.Synced
                ? LocalizationService.Instance["RolePanels.ActionApplied"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.StatusUpdateFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveRoleFieldsAsync()
    {
        if (SelectedTask is null || _session.UserId is null)
        {
            return;
        }

        ErrorMessage = null;
        StatusMessage = null;
        IsBusy = true;
        try
        {
            var values = ReadRoleFields(SelectedTask.RoleFieldsJson);
            foreach (var editor in FieldEditors)
            {
                var value = string.IsNullOrWhiteSpace(editor.Value) ? null : editor.Value.Trim();
                if (value is null)
                {
                    values.Remove(editor.Key);
                }
                else
                {
                    values[editor.Key] = value;
                }
            }

            var json = values.Count == 0 ? null : JsonSerializer.Serialize(values);
            var updated = await _workspace.UpdateTaskRoleFieldsAsync(_session.UserId.Value, SelectedTask.Id, json, _clientId);
            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.TaskSaveFailed"];
                return;
            }

            await LoadTasksAsync(updated.Value.Id);
            StatusMessage = updated.Synced
                ? LocalizationService.Instance["RolePanels.FieldsSaved"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.TaskSaveFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_session.UserId is null)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.NoSession"];
            return;
        }

        IsBusy = true;
        try
        {
            BuildRolePanels();
            var currentProjectId = SelectedProject?.Id;
            var currentRole = SelectedRolePanel?.Role;
            var projects = await _workspace.ListProjectsAsync(_session.UserId.Value);
            Projects.ReplaceWith(projects.Select(x => new ProjectItemViewModel(x)));
            OnPropertyChanged(nameof(HasProjects));

            _reloadingSelection = true;
            SelectedProject = currentProjectId is not null
                ? Projects.FirstOrDefault(x => x.Id == currentProjectId.Value)
                : Projects.FirstOrDefault();
            SelectedRolePanel = currentRole is not null
                ? RolePanels.FirstOrDefault(x => x.Role == currentRole.Value)
                : RolePanels.FirstOrDefault();
            _reloadingSelection = false;

            RefreshRoleContent();
            if (SelectedProject is not null && SelectedRolePanel is not null)
            {
                await _sync.SubscribeProjectAsync(SelectedProject.Id);
                await LoadTasksAsync();
            }
            else
            {
                Tasks.Clear();
                SelectedTask = null;
                OnPropertyChanged(nameof(HasTasks));
                BuildFieldEditors();
            }
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.LoadFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadTasksAsync(Guid? selectedTaskId = null)
    {
        if (_session.UserId is null || SelectedProject is null || SelectedRolePanel is null)
        {
            Tasks.Clear();
            SelectedTask = null;
            OnPropertyChanged(nameof(HasTasks));
            BuildFieldEditors();
            return;
        }

        var profile = SelectedRolePanel.Profile;
        var query = new TaskListQuery(
            SelectedProject.Id,
            profile.DefaultStatusFilter,
            null,
            null,
            profile.DefaultSort,
            profile.DefaultSortDirection,
            true);
        var tasks = await _workspace.ListTasksAsync(_session.UserId.Value, query);
        Tasks.ReplaceWith(tasks.Select(x => new TaskItemViewModel(x)));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(ShowEmptyTasksMessage));
        OnPropertyChanged(nameof(ShowTaskList));

        _reloadingSelection = true;
        SelectedTask = selectedTaskId is not null
            ? Tasks.FirstOrDefault(x => x.Id == selectedTaskId.Value)
            : Tasks.FirstOrDefault(x => SelectedTask is not null && x.Id == SelectedTask.Id) ?? Tasks.FirstOrDefault();
        _reloadingSelection = false;
        BuildFieldEditors();
    }

    private void OnProjectInvalidated(Guid projectId)
    {
        if (SelectedProject?.Id == projectId)
        {
            _ = LoadTasksAsync(SelectedTask?.Id);
        }
    }

    private void BuildRolePanels()
    {
        var roles = _session.Roles;
        RolePanels.ReplaceWith(RoleWorkspaceProfiles.ForRoles(roles).Select(x => new RolePanelTabViewModel(x)));
        OnPropertyChanged(nameof(HasRoles));
        OnPropertyChanged(nameof(ShowNoRolesMessage));
        OnPropertyChanged(nameof(ShowNoProjectMessage));
        OnPropertyChanged(nameof(ShowTaskList));
        OnPropertyChanged(nameof(ShowRoleEditor));
    }

    private void RefreshRoleContent()
    {
        DashboardCards.Clear();
        Actions.Clear();
        if (SelectedRolePanel is not null)
        {
            foreach (var card in SelectedRolePanel.Profile.DashboardCards)
            {
                DashboardCards.Add(new RoleDashboardCardViewModel(card));
            }

            foreach (var action in SelectedRolePanel.Profile.Actions)
            {
                Actions.Add(new RoleTaskActionViewModel(action));
            }
        }

        BuildFieldEditors();
    }

    private void BuildFieldEditors()
    {
        FieldEditors.Clear();
        if (SelectedRolePanel is null || SelectedTask is null)
        {
            return;
        }

        var values = ReadRoleFields(SelectedTask.RoleFieldsJson);
        foreach (var field in SelectedRolePanel.Profile.Fields)
        {
            values.TryGetValue(field.Key, out var value);
            FieldEditors.Add(new RoleFieldEditorViewModel(field, value));
        }
    }

    private static Dictionary<string, string?> ReadRoleFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return new Dictionary<string, string?>();
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, string?>>(json) ?? new Dictionary<string, string?>();
        }
        catch (JsonException)
        {
            return new Dictionary<string, string?>();
        }
    }
}

public sealed class RolePanelTabViewModel
{
    public RoleWorkspaceProfile Profile { get; }
    public RoleType Role => Profile.Role;
    public string Name => LocalizationService.Instance[Profile.NameKey];

    public RolePanelTabViewModel(RoleWorkspaceProfile profile)
    {
        Profile = profile;
    }
}

public sealed class RoleDashboardCardViewModel
{
    public string Title { get; }
    public string Description { get; }

    public RoleDashboardCardViewModel(RoleDashboardCard card)
    {
        Title = LocalizationService.Instance[card.TitleKey];
        Description = LocalizationService.Instance[card.DescriptionKey];
    }
}

public sealed class RoleTaskActionViewModel
{
    public TaskStatus TargetStatus { get; }
    public string Label { get; }
    public string Description { get; }

    public RoleTaskActionViewModel(RoleTaskAction action)
    {
        TargetStatus = Enum.TryParse<TaskStatus>(action.TargetStatus, out var status) ? status : TaskStatus.Todo;
        Label = LocalizationService.Instance[action.LabelKey];
        Description = LocalizationService.Instance[action.DescriptionKey];
    }
}

public sealed partial class RoleFieldEditorViewModel : ObservableObject
{
    public string Key { get; }
    public string Label { get; }
    public RoleFieldKind Kind { get; }
    public bool IsMultiline => Kind == RoleFieldKind.Multiline;
    public bool IsChoice => Kind == RoleFieldKind.Choice;
    public bool IsText => !IsChoice;
    public ObservableCollection<string> Options { get; } = new();

    [ObservableProperty] private string? _value;

    public RoleFieldEditorViewModel(RoleFieldDefinition definition, string? value)
    {
        Key = definition.Key;
        Label = LocalizationService.Instance[definition.LabelKey];
        Kind = definition.Kind;
        Value = value;
        foreach (var option in definition.Options ?? [])
        {
            Options.Add(option);
        }
    }
}
