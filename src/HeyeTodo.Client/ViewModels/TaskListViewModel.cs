using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using HeyeTodo.Client.Application.Tasks;
using HeyeTodo.Client.Application.Sync;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class TaskListViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceService _workspace;
    private readonly ISyncCoordinator _sync;
    private readonly ClientSession _session;
    private readonly Guid _clientId;
    private bool _reloadingSelection;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();
    public ObservableCollection<TaskStatusOption> StatusFilters { get; } = new();
    public ObservableCollection<TaskPriorityOption> PriorityFilters { get; } = new();
    public ObservableCollection<TaskSortOption> SortOptions { get; } = new();
    public ObservableCollection<TaskStatusOption> EditorStatuses { get; } = new();
    public ObservableCollection<TaskPriorityOption> EditorPriorities { get; } = new();

    [ObservableProperty] private ProjectItemViewModel? _selectedProject;
    [ObservableProperty] private TaskItemViewModel? _selectedTask;
    [ObservableProperty] private TaskStatusOption? _selectedStatusFilter;
    [ObservableProperty] private TaskPriorityOption? _selectedPriorityFilter;
    [ObservableProperty] private TaskSortOption? _selectedSort;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private bool _includeCompleted = true;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private string _projectName = string.Empty;
    [ObservableProperty] private string _projectDescription = string.Empty;
    [ObservableProperty] private bool _isEditorVisible;
    [ObservableProperty] private bool _isCreateMode;
    [ObservableProperty] private string _editTitle = string.Empty;
    [ObservableProperty] private string _editDescription = string.Empty;
    [ObservableProperty] private TaskStatusOption? _editStatus;
    [ObservableProperty] private TaskPriorityOption? _editPriority;
    [ObservableProperty] private string _editStartDate = string.Empty;
    [ObservableProperty] private string _editEndDate = string.Empty;
    [ObservableProperty] private string _editEstimatedHours = string.Empty;

    public bool HasProjects => Projects.Count > 0;
    public bool HasTasks => Tasks.Count > 0;
    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasSelectedTask => SelectedTask is not null;
    public bool ShowEditorForm => IsEditorVisible && HasSelectedProject;
    public bool ShowNoProjectMessage => !HasSelectedProject;
    public bool ShowEmptyTasksMessage => HasSelectedProject && !HasTasks;
    public bool ShowTaskList => HasSelectedProject && HasTasks;

    public TaskListViewModel(ITaskWorkspaceService workspace, ISyncCoordinator sync)
    {
        _workspace = workspace;
        _sync = sync;
        _session = AppHost.Services.GetRequiredService<ClientSession>();
        _clientId = AppPaths.GetOrCreateClientId();
        _sync.ProjectInvalidated += OnProjectInvalidated;

        StatusFilters.Add(new TaskStatusOption(null, "Tasks.Filter.Status.All"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.Backlog, "Tasks.Status.Backlog"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.Todo, "Tasks.Status.Todo"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.InProgress, "Tasks.Status.InProgress"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.Blocked, "Tasks.Status.Blocked"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.Review, "Tasks.Status.Review"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.Done, "Tasks.Status.Done"));
        StatusFilters.Add(new TaskStatusOption(TaskStatus.Cancelled, "Tasks.Status.Cancelled"));

        PriorityFilters.Add(new TaskPriorityOption(null, "Tasks.Filter.Priority.All"));
        PriorityFilters.Add(new TaskPriorityOption(TaskPriority.Low, "Tasks.Priority.Low"));
        PriorityFilters.Add(new TaskPriorityOption(TaskPriority.Normal, "Tasks.Priority.Normal"));
        PriorityFilters.Add(new TaskPriorityOption(TaskPriority.High, "Tasks.Priority.High"));
        PriorityFilters.Add(new TaskPriorityOption(TaskPriority.Critical, "Tasks.Priority.Critical"));

        SortOptions.Add(new TaskSortOption(TaskSortField.UpdatedAt, SortDirection.Descending, "Tasks.Sort.UpdatedDesc"));
        SortOptions.Add(new TaskSortOption(TaskSortField.Title, SortDirection.Ascending, "Tasks.Sort.TitleAsc"));
        SortOptions.Add(new TaskSortOption(TaskSortField.Priority, SortDirection.Descending, "Tasks.Sort.PriorityDesc"));
        SortOptions.Add(new TaskSortOption(TaskSortField.Status, SortDirection.Ascending, "Tasks.Sort.StatusAsc"));

        foreach (var item in StatusFilters.Where(x => x.Value is not null))
        {
            EditorStatuses.Add(item);
        }

        foreach (var item in PriorityFilters.Where(x => x.Value is not null))
        {
            EditorPriorities.Add(item);
        }

        SelectedStatusFilter = StatusFilters[0];
        SelectedPriorityFilter = PriorityFilters[0];
        SelectedSort = SortOptions[0];
        EditStatus = EditorStatuses.FirstOrDefault(x => x.Value == TaskStatus.Todo);
        EditPriority = EditorPriorities.FirstOrDefault(x => x.Value == TaskPriority.Normal);

        _ = LoadAsync();
    }

    partial void OnSelectedProjectChanged(ProjectItemViewModel? value)
    {
        var previousProjectId = SelectedProject?.Id;
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(ShowNoProjectMessage));
        OnPropertyChanged(nameof(ShowEmptyTasksMessage));
        OnPropertyChanged(nameof(ShowTaskList));
        if (_reloadingSelection)
        {
            return;
        }

        SelectedTask = null;
        if (value is null)
        {
            if (previousProjectId is not null)
            {
                _ = _sync.UnsubscribeProjectAsync(previousProjectId.Value);
            }
            Tasks.Clear();
            OnPropertyChanged(nameof(HasTasks));
            ResetEditor();
            ProjectName = string.Empty;
            ProjectDescription = string.Empty;
            return;
        }

        ProjectName = value.Name;
        ProjectDescription = value.Description ?? string.Empty;
        if (previousProjectId is not null && previousProjectId != value.Id)
        {
            _ = _sync.UnsubscribeProjectAsync(previousProjectId.Value);
        }
        _ = _sync.SubscribeProjectAsync(value.Id);
        ResetEditor();
        _ = LoadTasksAsync();
    }

    partial void OnSelectedTaskChanged(TaskItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedTask));
        if (_reloadingSelection)
        {
            return;
        }

        if (value is null)
        {
            if (!IsCreateMode)
            {
                ResetEditor();
            }
            return;
        }

        IsCreateMode = false;
        IsEditorVisible = true;
        EditTitle = value.Title;
        EditDescription = value.Description ?? string.Empty;
        EditStatus = EditorStatuses.FirstOrDefault(x => x.Value == value.Status) ?? EditorStatuses[0];
        EditPriority = EditorPriorities.FirstOrDefault(x => x.Value == value.Priority) ?? EditorPriorities[0];
        EditStartDate = FormatDate(value.StartDate);
        EditEndDate = FormatDate(value.EndDate);
        EditEstimatedHours = value.EstimatedHours?.ToString("0.##", CultureInfo.InvariantCulture) ?? string.Empty;
    }

    partial void OnSearchTextChanged(string value) => _ = LoadTasksAsync();
    partial void OnSelectedStatusFilterChanged(TaskStatusOption? value) => _ = LoadTasksAsync();
    partial void OnSelectedPriorityFilterChanged(TaskPriorityOption? value) => _ = LoadTasksAsync();
    partial void OnSelectedSortChanged(TaskSortOption? value) => _ = LoadTasksAsync();
    partial void OnIncludeCompletedChanged(bool value) => _ = LoadTasksAsync();

    [RelayCommand]
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
            var currentId = SelectedProject?.Id;
            var projects = await _workspace.ListProjectsAsync(_session.UserId.Value);
            Projects.ReplaceWith(projects.Select(x => new ProjectItemViewModel(x)));
            OnPropertyChanged(nameof(HasProjects));

            _reloadingSelection = true;
            SelectedProject = currentId is not null
                ? Projects.FirstOrDefault(x => x.Id == currentId.Value)
                : Projects.FirstOrDefault();
            _reloadingSelection = false;

            if (SelectedProject is null)
            {
                Tasks.Clear();
                OnPropertyChanged(nameof(HasTasks));
                OnPropertyChanged(nameof(ShowEmptyTasksMessage));
                OnPropertyChanged(nameof(ShowTaskList));
                ResetEditor();
                ProjectName = string.Empty;
                ProjectDescription = string.Empty;
                return;
            }

            ProjectName = SelectedProject.Name;
            ProjectDescription = SelectedProject.Description ?? string.Empty;
            await LoadTasksAsync();
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

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadAsync();
    }

    private void OnProjectInvalidated(Guid projectId)
    {
        if (SelectedProject?.Id == projectId)
        {
            _ = LoadTasksAsync(SelectedTask?.Id);
        }
    }

    [RelayCommand]
    private async Task CreateProjectAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_session.UserId is null)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.NoSession"];
            return;
        }

        var name = ProjectName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectNameRequired"];
            return;
        }

        IsBusy = true;
        try
        {
            var project = await _workspace.CreateProjectAsync(
                _session.UserId.Value,
                new CreateProjectRequest(name, NormalizeNullable(ProjectDescription)),
                _clientId);

            await LoadAsync();
            _reloadingSelection = true;
            SelectedProject = Projects.FirstOrDefault(x => x.Id == project.Value.Id);
            _reloadingSelection = false;
            ProjectName = SelectedProject?.Name ?? string.Empty;
            ProjectDescription = SelectedProject?.Description ?? string.Empty;
            StatusMessage = project.Synced
                ? LocalizationService.Instance["Tasks.ProjectCreated"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectCreateFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SaveProjectAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_session.UserId is null || SelectedProject is null)
        {
            return;
        }

        var name = ProjectName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectNameRequired"];
            return;
        }

        IsBusy = true;
        try
        {
            var updated = await _workspace.UpdateProjectAsync(
                _session.UserId.Value,
                SelectedProject.Id,
                new UpdateProjectRequest(SelectedProject.Id, name, NormalizeNullable(ProjectDescription)),
                _clientId);

            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectUpdateFailed"];
                return;
            }

            await LoadAsync();
            _reloadingSelection = true;
            SelectedProject = Projects.FirstOrDefault(x => x.Id == updated.Value.Id);
            _reloadingSelection = false;
            ProjectName = SelectedProject?.Name ?? string.Empty;
            ProjectDescription = SelectedProject?.Description ?? string.Empty;
            StatusMessage = updated.Synced
                ? LocalizationService.Instance["Tasks.ProjectSaved"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectUpdateFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task DeleteProjectAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_session.UserId is null || SelectedProject is null)
        {
            return;
        }

        var deletedId = SelectedProject.Id;
        IsBusy = true;
        try
        {
            var deleted = await _workspace.DeleteProjectAsync(_session.UserId.Value, deletedId, _clientId);
            if (!deleted.Success)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectDeleteFailed"];
                return;
            }

            await LoadAsync();
            StatusMessage = deleted.Synced
                ? LocalizationService.Instance["Tasks.ProjectDeleted"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.ProjectDeleteFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void NewTask()
    {
        if (SelectedProject is null)
        {
            return;
        }

        _reloadingSelection = true;
        SelectedTask = null;
        _reloadingSelection = false;

        IsCreateMode = true;
        IsEditorVisible = true;
        EditTitle = string.Empty;
        EditDescription = string.Empty;
        EditStatus = EditorStatuses.FirstOrDefault(x => x.Value == TaskStatus.Todo) ?? EditorStatuses[0];
        EditPriority = EditorPriorities.FirstOrDefault(x => x.Value == TaskPriority.Normal) ?? EditorPriorities[0];
        EditStartDate = string.Empty;
        EditEndDate = string.Empty;
        EditEstimatedHours = string.Empty;
        ErrorMessage = null;
        StatusMessage = null;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        if (SelectedTask is not null)
        {
            OnSelectedTaskChanged(SelectedTask);
            return;
        }

        ResetEditor();
    }

    [RelayCommand]
    private async Task SaveTaskAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_session.UserId is null || SelectedProject is null)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.NoProjectSelected"];
            return;
        }

        if (string.IsNullOrWhiteSpace(EditTitle))
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.TitleRequired"];
            return;
        }

        if (!TryParseDates(out var startDate, out var endDate) || !TryParseEstimate(out var estimate))
        {
            return;
        }

        IsBusy = true;
        try
        {
            if (IsCreateMode || SelectedTask is null)
            {
                var created = await _workspace.CreateTaskAsync(
                    _session.UserId.Value,
                    new CreateTaskRequest(
                        SelectedProject.Id,
                        EditTitle.Trim(),
                        NormalizeNullable(EditDescription),
                        EditStatus?.Value ?? TaskStatus.Todo,
                        EditPriority?.Value ?? TaskPriority.Normal,
                        startDate,
                        endDate,
                        estimate,
                        null,
                        null),
                    _clientId);

                await LoadTasksAsync(created.Value.Id);
                StatusMessage = created.Synced
                    ? LocalizationService.Instance["Tasks.TaskCreated"]
                    : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
                return;
            }

            var updated = await _workspace.UpdateTaskAsync(
                _session.UserId.Value,
                SelectedTask.Id,
                new UpdateTaskRequest(
                    SelectedTask.Id,
                    SelectedProject.Id,
                    EditTitle.Trim(),
                    NormalizeNullable(EditDescription),
                    EditStatus?.Value ?? TaskStatus.Todo,
                    EditPriority?.Value ?? TaskPriority.Normal,
                    startDate,
                    endDate,
                    estimate,
                    null,
                    null),
                _clientId);

            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.TaskSaveFailed"];
                return;
            }

            await LoadTasksAsync(updated.Value.Id);
            StatusMessage = updated.Synced
                ? LocalizationService.Instance["Tasks.TaskSaved"]
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

    [RelayCommand]
    private async Task DeleteTaskAsync()
    {
        ErrorMessage = null;
        StatusMessage = null;

        if (_session.UserId is null || SelectedTask is null)
        {
            return;
        }

        var deletedId = SelectedTask.Id;
        IsBusy = true;
        try
        {
            var deleted = await _workspace.DeleteTaskAsync(_session.UserId.Value, deletedId, _clientId);
            if (!deleted.Success)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.TaskDeleteFailed"];
                return;
            }

            await LoadTasksAsync();
            ResetEditor();
            StatusMessage = deleted.Synced
                ? LocalizationService.Instance["Tasks.TaskDeleted"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.TaskDeleteFailed"];
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task AdvanceTaskStatusAsync(TaskItemViewModel? task)
    {
        if (_session.UserId is null || task is null)
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
                task.Id,
                new ChangeTaskStatusRequest(task.Id, GetNextStatus(task.Status)),
                _clientId);

            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.StatusUpdateFailed"];
                return;
            }

            await LoadTasksAsync(updated.Value.Id);
            if (!updated.Synced)
            {
                StatusMessage = LocalizationService.Instance["Tasks.Warning.LocalOnly"];
            }
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

    private async Task LoadTasksAsync(Guid? selectTaskId = null)
    {
        ErrorMessage = null;

        if (_session.UserId is null || SelectedProject is null)
        {
            Tasks.Clear();
            OnPropertyChanged(nameof(HasTasks));
            return;
        }

        var query = new TaskListQuery(
            SelectedProject.Id,
            SelectedStatusFilter?.Value,
            SelectedPriorityFilter?.Value,
            NormalizeNullable(SearchText),
            SelectedSort?.SortBy ?? TaskSortField.UpdatedAt,
            SelectedSort?.Direction ?? SortDirection.Descending,
            IncludeCompleted);

        var list = await _workspace.ListTasksAsync(_session.UserId.Value, query);
        Tasks.ReplaceWith(list.Select(x => new TaskItemViewModel(x)));
        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(ShowEmptyTasksMessage));
        OnPropertyChanged(nameof(ShowTaskList));

        _reloadingSelection = true;
        SelectedTask = selectTaskId is not null
            ? Tasks.FirstOrDefault(x => x.Id == selectTaskId.Value)
            : Tasks.FirstOrDefault(x => SelectedTask is not null && x.Id == SelectedTask.Id);
        _reloadingSelection = false;

        if (SelectedTask is not null)
        {
            OnSelectedTaskChanged(SelectedTask);
        }
        else if (!IsCreateMode)
        {
            ResetEditor();
        }
    }

    private void ResetEditor()
    {
        IsEditorVisible = false;
        IsCreateMode = false;
        EditTitle = string.Empty;
        EditDescription = string.Empty;
        EditStatus = EditorStatuses.FirstOrDefault(x => x.Value == TaskStatus.Todo);
        EditPriority = EditorPriorities.FirstOrDefault(x => x.Value == TaskPriority.Normal);
        EditStartDate = string.Empty;
        EditEndDate = string.Empty;
        EditEstimatedHours = string.Empty;
    }

    private bool TryParseDates(out DateTimeOffset? startDate, out DateTimeOffset? endDate)
    {
        startDate = null;
        endDate = null;

        if (!TryParseDate(EditStartDate, out startDate))
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.InvalidStartDate"];
            return false;
        }

        if (!TryParseDate(EditEndDate, out endDate))
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.InvalidEndDate"];
            return false;
        }

        return true;
    }

    private bool TryParseEstimate(out double? estimate)
    {
        estimate = null;
        if (string.IsNullOrWhiteSpace(EditEstimatedHours))
        {
            return true;
        }

        if (!double.TryParse(EditEstimatedHours.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed))
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.InvalidEstimate"];
            return false;
        }

        estimate = parsed;
        return true;
    }

    private static bool TryParseDate(string text, out DateTimeOffset? value)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            value = null;
            return true;
        }

        if (DateTimeOffset.TryParse(text.Trim(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
        {
            value = parsed;
            return true;
        }

        value = null;
        return false;
    }

    private static string FormatDate(DateTimeOffset? value)
        => value?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty;

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static TaskStatus GetNextStatus(TaskStatus status)
        => status switch
        {
            TaskStatus.Backlog => TaskStatus.Todo,
            TaskStatus.Todo => TaskStatus.InProgress,
            TaskStatus.InProgress => TaskStatus.Review,
            TaskStatus.Review => TaskStatus.Done,
            TaskStatus.Blocked => TaskStatus.InProgress,
            TaskStatus.Done => TaskStatus.Todo,
            TaskStatus.Cancelled => TaskStatus.Backlog,
            _ => TaskStatus.Todo,
        };
}

public sealed class ProjectItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Name { get; }
    public string? Description { get; }

    public ProjectItemViewModel(LocalProject project)
    {
        Id = project.Id;
        Name = project.Name;
        Description = project.Description;
    }
}

public sealed class TaskItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public TaskStatus Status { get; }
    public TaskPriority Priority { get; }
    public DateTimeOffset? StartDate { get; }
    public DateTimeOffset? EndDate { get; }
    public double? EstimatedHours { get; }
    public string? RoleFieldsJson { get; }
    public string StatusLabel => LocalizationService.Instance[TaskStatusOption.KeyFor(Status)];
    public string PriorityLabel => LocalizationService.Instance[TaskPriorityOption.KeyFor(Priority)];
    public string ScheduleLabel => BuildScheduleLabel(StartDate, EndDate);

    public TaskItemViewModel(LocalTask task)
    {
        Id = task.Id;
        Title = task.Title;
        Description = task.Description;
        Status = task.Status;
        Priority = task.Priority;
        StartDate = task.StartDate;
        EndDate = task.EndDate;
        EstimatedHours = task.EstimatedHours;
        RoleFieldsJson = task.RoleFieldsJson;
    }

    private static string BuildScheduleLabel(DateTimeOffset? start, DateTimeOffset? end)
    {
        var startText = start?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var endText = end?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        return (startText, endText) switch
        {
            (null, null) => LocalizationService.Instance["Tasks.Schedule.None"],
            (not null, null) => startText!,
            (null, not null) => endText!,
            _ => $"{startText} - {endText}",
        };
    }
}

public sealed class TaskStatusOption : OptionItem
{
    public new TaskStatus? Value { get; }

    public TaskStatusOption(TaskStatus? value, string labelKey) : base(value?.ToString() ?? string.Empty, labelKey)
    {
        Value = value;
    }

    public static string KeyFor(TaskStatus status)
        => status switch
        {
            TaskStatus.Backlog => "Tasks.Status.Backlog",
            TaskStatus.Todo => "Tasks.Status.Todo",
            TaskStatus.InProgress => "Tasks.Status.InProgress",
            TaskStatus.Blocked => "Tasks.Status.Blocked",
            TaskStatus.Review => "Tasks.Status.Review",
            TaskStatus.Done => "Tasks.Status.Done",
            TaskStatus.Cancelled => "Tasks.Status.Cancelled",
            _ => "Tasks.Status.Todo",
        };
}

public sealed class TaskPriorityOption : OptionItem
{
    public new TaskPriority? Value { get; }

    public TaskPriorityOption(TaskPriority? value, string labelKey) : base(value?.ToString() ?? string.Empty, labelKey)
    {
        Value = value;
    }

    public static string KeyFor(TaskPriority priority)
        => priority switch
        {
            TaskPriority.Low => "Tasks.Priority.Low",
            TaskPriority.Normal => "Tasks.Priority.Normal",
            TaskPriority.High => "Tasks.Priority.High",
            TaskPriority.Critical => "Tasks.Priority.Critical",
            _ => "Tasks.Priority.Normal",
        };
}

public sealed class TaskSortOption : OptionItem
{
    public TaskSortField SortBy { get; }
    public SortDirection Direction { get; }

    public TaskSortOption(TaskSortField sortBy, SortDirection direction, string labelKey) : base($"{sortBy}:{direction}", labelKey)
    {
        SortBy = sortBy;
        Direction = direction;
    }
}

internal static class ObservableCollectionExtensions
{
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
