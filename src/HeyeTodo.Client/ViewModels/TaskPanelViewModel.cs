using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Persistence;
using HeyeTodo.Client.Services;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class TaskPanelViewModel : ViewModelBase
{
    private const string AllProjectsId = "__all__";
    private const string ProjectsModuleId = "task.projects";
    private const string TasksModuleId = "task.items";

    private readonly IPersistenceStore _persistenceStore;
    private readonly ITaskRepository _taskRepository;
    private readonly SyncCoordinator _syncCoordinator;
    private CancellationTokenSource? _projectsSaveCancellationTokenSource;
    private CancellationTokenSource? _tasksSaveCancellationTokenSource;
    private bool _isLoading;

    public TaskPanelViewModel(IPersistenceStore persistenceStore, ITaskRepository taskRepository, SyncCoordinator syncCoordinator)
    {
        _persistenceStore = persistenceStore;
        _taskRepository = taskRepository;
        _syncCoordinator = syncCoordinator;
        ProjectFilters.Add(new ProjectFilterViewModel(AllProjectsId, "全部"));
        SelectedProjectFilter = ProjectFilters[0];
        _ = LoadAsync();
    }

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();

    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();

    public ObservableCollection<TaskItemViewModel> VisibleTasks { get; } = new();

    public ObservableCollection<ProjectFilterViewModel> ProjectFilters { get; } = new();

    public ObservableCollection<TaskUrgencyOptionViewModel> UrgencyOptions { get; } = new()
    {
        new(TaskUrgencyLevel.Low, "低"),
        new(TaskUrgencyLevel.Medium, "中"),
        new(TaskUrgencyLevel.High, "高"),
        new(TaskUrgencyLevel.Urgent, "紧急")
    };

    public bool HasNoVisibleTasks => VisibleTasks.Count == 0;

    public string SelectedTaskName => SelectedTask?.Name ?? string.Empty;

    public string SelectedTaskDescription => SelectedTask?.Description ?? string.Empty;

    public string SelectedTaskProjectName => SelectedTask?.ProjectName ?? string.Empty;

    public string SelectedTaskStartTimeText => SelectedTask?.StartTimeText ?? "未设置";

    public string SelectedTaskEndTimeText => SelectedTask?.EndTimeText ?? "未设置";

    public string SelectedTaskAssigneeNameText => SelectedTask?.AssigneeNameText ?? "未设置";

    public string SelectedTaskUrgencyName => SelectedTask?.UrgencyName ?? "中";

    public bool HasSelectedTaskTimeWarning => SelectedTask?.StartTime is not null && SelectedTask.EndTime is not null && SelectedTask.StartTime > SelectedTask.EndTime;

    public bool HasNewTaskTimeWarning => !string.IsNullOrWhiteSpace(NewTaskTimeWarning);

    [ObservableProperty]
    private ProjectFilterViewModel? _selectedProjectFilter;

    [ObservableProperty]
    private TaskItemViewModel? _selectedTask;

    [ObservableProperty]
    private bool _isCreateProjectDialogOpen;

    [ObservableProperty]
    private bool _isCreateTaskDialogOpen;

    [ObservableProperty]
    private bool _isDeleteConfirmOpen;

    [ObservableProperty]
    private bool _isDetailsOpen;

    [ObservableProperty]
    private string _newProjectName = string.Empty;

    [ObservableProperty]
    private string _newProjectDescription = string.Empty;

    [ObservableProperty]
    private string _newTaskName = string.Empty;

    [ObservableProperty]
    private string _newTaskDescription = string.Empty;

    [ObservableProperty]
    private DateTime? _newTaskStartDate;

    [ObservableProperty]
    private DateTime? _newTaskEndDate;

    [ObservableProperty]
    private string _newTaskAssigneeName = string.Empty;

    [ObservableProperty]
    private string _newTaskTimeWarning = string.Empty;

    [ObservableProperty]
    private TaskUrgencyOptionViewModel? _newTaskUrgency;

    [ObservableProperty]
    private ProjectItemViewModel? _newTaskProject;

    [ObservableProperty]
    private string _statusMessage = "正在读取任务存档";

    [ObservableProperty]
    private DateTime? _selectedTaskStartDate;

    [ObservableProperty]
    private DateTime? _selectedTaskEndDate;

    [ObservableProperty]
    private string _selectedTaskAssigneeName = string.Empty;

    [ObservableProperty]
    private TaskUrgencyOptionViewModel? _selectedTaskUrgency;

    partial void OnSelectedProjectFilterChanged(ProjectFilterViewModel? value)
    {
        RefreshVisibleTasks();
    }

    partial void OnSelectedTaskChanged(TaskItemViewModel? value)
    {
        OnPropertyChanged(nameof(SelectedTaskName));
        OnPropertyChanged(nameof(SelectedTaskDescription));
        OnPropertyChanged(nameof(SelectedTaskProjectName));
        OnPropertyChanged(nameof(SelectedTaskStartTimeText));
        OnPropertyChanged(nameof(SelectedTaskEndTimeText));
        OnPropertyChanged(nameof(SelectedTaskAssigneeNameText));
        OnPropertyChanged(nameof(SelectedTaskUrgencyName));
        OnPropertyChanged(nameof(HasSelectedTaskTimeWarning));

        _isLoading = true;
        SelectedTaskStartDate = ToDateTime(value?.StartTime);
        SelectedTaskEndDate = ToDateTime(value?.EndTime);
        SelectedTaskAssigneeName = value?.AssigneeName ?? string.Empty;
        SelectedTaskUrgency = ResolveUrgencyOption(value?.Urgency ?? TaskUrgencyLevel.Medium);
        _isLoading = false;
    }

    partial void OnSelectedTaskStartDateChanged(DateTime? value)
    {
        if (SelectedTask is null || _isLoading)
        {
            return;
        }

        SelectedTask.StartTime = ToDateTimeOffset(value);
        SelectedTask.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedTaskDetailsChanged();
        QueueTaskSave(SelectedTask);
    }

    partial void OnSelectedTaskEndDateChanged(DateTime? value)
    {
        if (SelectedTask is null || _isLoading)
        {
            return;
        }

        SelectedTask.EndTime = ToDateTimeOffset(value);
        SelectedTask.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedTaskDetailsChanged();
        QueueTaskSave(SelectedTask);
    }

    partial void OnSelectedTaskAssigneeNameChanged(string value)
    {
        if (SelectedTask is null || _isLoading)
        {
            return;
        }

        SelectedTask.AssigneeName = value.Trim();
        SelectedTask.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedTaskDetailsChanged();
        QueueTaskSave(SelectedTask);
    }

    partial void OnSelectedTaskUrgencyChanged(TaskUrgencyOptionViewModel? value)
    {
        if (SelectedTask is null || _isLoading || value is null)
        {
            return;
        }

        SelectedTask.Urgency = value.Value;
        SelectedTask.UpdatedAt = DateTimeOffset.UtcNow;
        NotifySelectedTaskDetailsChanged();
        QueueTaskSave(SelectedTask);
    }

    partial void OnNewTaskStartDateChanged(DateTime? value)
    {
        UpdateNewTaskTimeWarning();
    }

    partial void OnNewTaskEndDateChanged(DateTime? value)
    {
        UpdateNewTaskTimeWarning();
    }

    [RelayCommand]
    private void SubmitSearch()
    {
        StatusMessage = "搜索功能暂未启用";
    }

    [RelayCommand]
    private async Task RefreshTasks()
    {
        RefreshVisibleTasks();
        QueueAllTasksSave();
        StatusMessage = "正在同步";
        try
        {
            StatusMessage = await _syncCoordinator.SyncOnceAsync();
            await ReloadFromRepositoryAsync();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            StatusMessage = $"同步失败，本地任务已保存：{exception.Message}";
        }
    }

    [RelayCommand]
    private void OpenCreateProjectDialog()
    {
        NewProjectName = string.Empty;
        NewProjectDescription = string.Empty;
        IsCreateProjectDialogOpen = true;
    }

    [RelayCommand]
    private void CancelCreateProject()
    {
        IsCreateProjectDialogOpen = false;
    }

    [RelayCommand]
    private void CreateProject()
    {
        var name = NewProjectName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "请先填写项目名称";
            return;
        }

        var project = new ProjectItemViewModel(name, NewProjectDescription.Trim());
        Projects.Add(project);
        ProjectFilters.Add(new ProjectFilterViewModel(project.Id, project.Name));
        IsCreateProjectDialogOpen = false;
        StatusMessage = "项目已创建";
        QueueProjectSave(project);
    }

    [RelayCommand]
    private void OpenCreateTaskDialog()
    {
        if (Projects.Count == 0)
        {
            StatusMessage = "请先创建项目";
            return;
        }

        NewTaskName = string.Empty;
        NewTaskDescription = string.Empty;
        NewTaskStartDate = null;
        NewTaskEndDate = null;
        NewTaskAssigneeName = string.Empty;
        NewTaskTimeWarning = string.Empty;
        NewTaskUrgency = ResolveUrgencyOption(TaskUrgencyLevel.Medium);
        NewTaskProject = ResolveDefaultTaskProject();
        IsCreateTaskDialogOpen = true;
    }

    [RelayCommand]
    private void CancelCreateTask()
    {
        IsCreateTaskDialogOpen = false;
    }

    [RelayCommand]
    private void CreateTask()
    {
        var name = NewTaskName.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            StatusMessage = "请先填写任务名称";
            return;
        }

        if (NewTaskProject is null)
        {
            StatusMessage = "请先选择任务所属项目";
            return;
        }

        var task = new TaskItemViewModel(NewTaskProject.Id, NewTaskProject.Name, name, NewTaskDescription.Trim())
        {
            StartTime = ToDateTimeOffset(NewTaskStartDate),
            EndTime = ToDateTimeOffset(NewTaskEndDate),
            AssigneeName = NewTaskAssigneeName.Trim(),
            Urgency = NewTaskUrgency?.Value ?? TaskUrgencyLevel.Medium
        };
        Tasks.Add(task);
        IsCreateTaskDialogOpen = false;
        StatusMessage = "任务已创建";
        RefreshVisibleTasks();
        QueueTaskSave(task);
    }

    [RelayCommand]
    public void SelectTask(TaskItemViewModel task)
    {
        SelectedTask = task;
        IsDetailsOpen = true;
    }

    [RelayCommand]
    private void CloseDetails()
    {
        IsDetailsOpen = false;
        SelectedTask = null;
    }

    [RelayCommand]
    private void ToggleTaskCompleted(TaskItemViewModel task)
    {
        task.IsCompleted = !task.IsCompleted;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        StatusMessage = task.IsCompleted ? "任务已完成" : "任务已恢复为未完成";
        QueueTaskSave(task);
    }

    public void UpdateTaskSchedule(TaskItemViewModel task, DateTime start, DateTime end)
    {
        task.StartTime = ToDateTimeOffset(start);
        task.EndTime = ToDateTimeOffset(end);
        task.UpdatedAt = DateTimeOffset.UtcNow;

        if (SelectedTask == task)
        {
            _isLoading = true;
            SelectedTaskStartDate = start;
            SelectedTaskEndDate = end;
            _isLoading = false;
            NotifySelectedTaskDetailsChanged();
        }

        RefreshVisibleTasks();
        StatusMessage = "任务时间已更新";
        QueueTaskSave(task);
    }

    [RelayCommand]
    private void RequestDeleteTask()
    {
        if (SelectedTask is not null)
        {
            IsDeleteConfirmOpen = true;
        }
    }

    [RelayCommand]
    private void CancelDeleteTask()
    {
        IsDeleteConfirmOpen = false;
    }

    [RelayCommand]
    private void ConfirmDeleteTask()
    {
        if (SelectedTask is null)
        {
            return;
        }

        var taskToDelete = SelectedTask;
        Tasks.Remove(taskToDelete);
        VisibleTasks.Remove(taskToDelete);
        SelectedTask = null;
        IsDetailsOpen = false;
        IsDeleteConfirmOpen = false;
        StatusMessage = "任务已删除";
        OnPropertyChanged(nameof(HasNoVisibleTasks));
        _ = DeleteTaskAsync(taskToDelete);
    }

    private async Task LoadAsync()
    {
        _isLoading = true;
        var hasError = false;

        try
        {
            await _taskRepository.InitializeAsync();
            var workspace = await _taskRepository.LoadWorkspaceAsync();
            if (workspace.Projects.Count == 0 && workspace.Tasks.Count == 0)
            {
                workspace = await TryImportLegacyJsonAsync();
            }

            foreach (var project in workspace.Projects)
            {
                if (string.IsNullOrWhiteSpace(project.Id))
                {
                    continue;
                }

                Projects.Add(new ProjectItemViewModel(project.Id, project.Name, project.Description)
                {
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt,
                    ServerVersion = project.ServerVersion
                });
            }

            RebuildProjectFilters();

            foreach (var task in workspace.Tasks)
            {
                if (string.IsNullOrWhiteSpace(task.Id))
                {
                    continue;
                }

                var projectName = ResolveProjectName(task.ProjectId, task.ProjectNameSnapshot);
                var taskViewModel = new TaskItemViewModel(task.Id, task.ProjectId, projectName, task.Name, task.Description, task.CreatedAt)
                {
                    IsCompleted = task.IsCompleted,
                    SortId = task.SortId,
                    StartTime = task.StartTime,
                    EndTime = task.EndTime,
                    AssigneeName = task.AssigneeName,
                    Urgency = task.Urgency,
                    UpdatedAt = task.UpdatedAt,
                    ServerVersion = task.ServerVersion
                };

                Tasks.Add(taskViewModel);
            }
        }
        catch (Exception exception) when (exception is PersistenceException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            hasError = true;
            StatusMessage = $"任务数据库读取失败：{exception.Message}";
        }
        finally
        {
            _isLoading = false;
        }

        RefreshVisibleTasks(false);

        if (!hasError)
        {
            StatusMessage = Projects.Count == 0 && Tasks.Count == 0 ? "还没有任务存档" : "任务存档已读取";
        }
    }

    private ProjectItemViewModel? ResolveDefaultTaskProject()
    {
        if (SelectedProjectFilter is not null && SelectedProjectFilter.Id != AllProjectsId)
        {
            return Projects.FirstOrDefault(project => project.Id == SelectedProjectFilter.Id);
        }

        return Projects.FirstOrDefault();
    }

    private TaskUrgencyOptionViewModel? ResolveUrgencyOption(TaskUrgencyLevel urgency)
    {
        return UrgencyOptions.FirstOrDefault(option => option.Value == urgency) ?? UrgencyOptions.FirstOrDefault(option => option.Value == TaskUrgencyLevel.Medium);
    }

    private void UpdateNewTaskTimeWarning()
    {
        NewTaskTimeWarning = NewTaskStartDate is not null && NewTaskEndDate is not null && NewTaskStartDate > NewTaskEndDate
            ? "开始时间晚于结束时间，请确认时间是否正确"
            : string.Empty;
        OnPropertyChanged(nameof(HasNewTaskTimeWarning));
    }

    private static DateTime? ToDateTime(DateTimeOffset? value)
    {
        return value?.LocalDateTime;
    }

    private static DateTimeOffset? ToDateTimeOffset(DateTime? value)
    {
        return value is null ? null : new DateTimeOffset(DateTime.SpecifyKind(value.Value, DateTimeKind.Local));
    }

    private void NotifySelectedTaskDetailsChanged()
    {
        OnPropertyChanged(nameof(SelectedTaskStartTimeText));
        OnPropertyChanged(nameof(SelectedTaskEndTimeText));
        OnPropertyChanged(nameof(SelectedTaskAssigneeNameText));
        OnPropertyChanged(nameof(SelectedTaskUrgencyName));
        OnPropertyChanged(nameof(HasSelectedTaskTimeWarning));
    }

    private string ResolveProjectName(string projectId, string projectNameSnapshot)
    {
        var project = Projects.FirstOrDefault(item => item.Id == projectId);
        if (project is not null)
        {
            return project.Name;
        }

        return string.IsNullOrWhiteSpace(projectNameSnapshot) ? "未知项目" : projectNameSnapshot;
    }

    private void RebuildProjectFilters()
    {
        ProjectFilters.Clear();
        ProjectFilters.Add(new ProjectFilterViewModel(AllProjectsId, "全部"));

        foreach (var project in Projects)
        {
            ProjectFilters.Add(new ProjectFilterViewModel(project.Id, project.Name));
        }

        SelectedProjectFilter = ProjectFilters[0];
    }

    private void RefreshVisibleTasks(bool rebuildSortIds = true)
    {
        if (rebuildSortIds)
        {
            RebuildSortIds();
        }

        VisibleTasks.Clear();

        var selectedProjectId = SelectedProjectFilter?.Id ?? AllProjectsId;
        var visibleTasks = Tasks
            .Where(task => selectedProjectId == AllProjectsId || task.ProjectId == selectedProjectId)
            .OrderBy(task => task.SortId)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        foreach (var task in visibleTasks)
        {
            VisibleTasks.Add(task);
        }

        OnPropertyChanged(nameof(HasNoVisibleTasks));
    }

    private void RebuildSortIds()
    {
        var orderedTasks = Tasks
            .OrderBy(task => task.EndTime is null)
            .ThenBy(task => task.EndTime)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        for (var i = 0; i < orderedTasks.Count; i++)
        {
            orderedTasks[i].SortId = i + 1;
        }
    }

    private async Task<TaskWorkspaceState> TryImportLegacyJsonAsync()
    {
        var projectsState = await _persistenceStore.LoadAsync<TaskProjectsPersistenceState>(ProjectsModuleId);
        var tasksState = await _persistenceStore.LoadAsync<TaskItemsPersistenceState>(TasksModuleId);

        if (projectsState is null && tasksState is null)
        {
            return new TaskWorkspaceState([], []);
        }

        var projects = (projectsState?.Projects ?? [])
            .Where(project => !string.IsNullOrWhiteSpace(project.Id))
            .Select(project =>
            {
                var now = DateTimeOffset.UtcNow;
                return new TaskProjectRecord(project.Id, project.Name, project.Description, now, now, 0, null);
            })
            .ToList();

        var tasks = (tasksState?.Tasks ?? [])
            .Where(task => !string.IsNullOrWhiteSpace(task.Id))
            .Select(task =>
            {
                var now = DateTimeOffset.UtcNow;
                return new TaskItemRecord(
                    task.Id,
                    task.ProjectId,
                    task.ProjectNameSnapshot,
                    task.Name,
                    task.Description,
                    task.IsCompleted,
                    task.SortId,
                    task.CreatedAt,
                    now,
                    task.StartTime,
                    task.EndTime,
                    task.AssigneeName,
                    task.Urgency,
                    0,
                    null);
            })
            .ToList();

        var imported = new TaskWorkspaceState(projects, tasks);
        await _taskRepository.ReplaceWorkspaceFromLocalImportAsync(imported);
        return imported;
    }

    private async Task ReloadFromRepositoryAsync()
    {
        _isLoading = true;
        try
        {
            var workspace = await _taskRepository.LoadWorkspaceAsync();
            Projects.Clear();
            Tasks.Clear();
            VisibleTasks.Clear();

            foreach (var project in workspace.Projects)
            {
                Projects.Add(new ProjectItemViewModel(project.Id, project.Name, project.Description)
                {
                    CreatedAt = project.CreatedAt,
                    UpdatedAt = project.UpdatedAt,
                    ServerVersion = project.ServerVersion
                });
            }

            RebuildProjectFilters();

            foreach (var task in workspace.Tasks)
            {
                var projectName = ResolveProjectName(task.ProjectId, task.ProjectNameSnapshot);
                Tasks.Add(new TaskItemViewModel(task.Id, task.ProjectId, projectName, task.Name, task.Description, task.CreatedAt)
                {
                    IsCompleted = task.IsCompleted,
                    SortId = task.SortId,
                    StartTime = task.StartTime,
                    EndTime = task.EndTime,
                    AssigneeName = task.AssigneeName,
                    Urgency = task.Urgency,
                    UpdatedAt = task.UpdatedAt,
                    ServerVersion = task.ServerVersion
                });
            }

            RefreshVisibleTasks(false);
        }
        finally
        {
            _isLoading = false;
        }
    }

    private void QueueProjectSave(ProjectItemViewModel project)
    {
        if (_isLoading)
        {
            return;
        }

        _projectsSaveCancellationTokenSource?.Cancel();
        _projectsSaveCancellationTokenSource?.Dispose();
        _projectsSaveCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _projectsSaveCancellationTokenSource.Token;

        _ = SaveProjectAfterDelayAsync(project, cancellationToken);
    }

    private void QueueTaskSave(TaskItemViewModel task)
    {
        if (_isLoading)
        {
            return;
        }

        _tasksSaveCancellationTokenSource?.Cancel();
        _tasksSaveCancellationTokenSource?.Dispose();
        _tasksSaveCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _tasksSaveCancellationTokenSource.Token;

        _ = SaveTaskAfterDelayAsync(task, cancellationToken);
    }

    private void QueueAllTasksSave()
    {
        if (_isLoading)
        {
            return;
        }

        _tasksSaveCancellationTokenSource?.Cancel();
        _tasksSaveCancellationTokenSource?.Dispose();
        _tasksSaveCancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _tasksSaveCancellationTokenSource.Token;

        _ = SaveAllTasksAfterDelayAsync(cancellationToken);
    }

    private async Task SaveProjectAfterDelayAsync(ProjectItemViewModel project, CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = "正在保存项目";
            await Task.Delay(400, cancellationToken);
            project.UpdatedAt = DateTimeOffset.UtcNow;
            await _taskRepository.SaveProjectAsync(ToProjectRecord(project), enqueueSyncChange: true, cancellationToken);
            StatusMessage = "项目已保存";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is PersistenceException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            StatusMessage = $"项目保存失败：{exception.Message}";
        }
    }

    private async Task SaveTaskAfterDelayAsync(TaskItemViewModel task, CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = "正在保存任务";
            await Task.Delay(400, cancellationToken);
            await _taskRepository.SaveTaskAsync(ToTaskRecord(task), enqueueSyncChange: true, cancellationToken);
            StatusMessage = "任务已保存";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is PersistenceException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            StatusMessage = $"任务保存失败：{exception.Message}";
        }
    }

    private async Task SaveAllTasksAfterDelayAsync(CancellationToken cancellationToken)
    {
        try
        {
            StatusMessage = "正在保存任务";
            await Task.Delay(400, cancellationToken);
            foreach (var task in Tasks)
            {
                task.UpdatedAt = DateTimeOffset.UtcNow;
                await _taskRepository.SaveTaskAsync(ToTaskRecord(task), enqueueSyncChange: true, cancellationToken);
            }

            StatusMessage = "任务已保存";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception) when (exception is PersistenceException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            StatusMessage = $"任务保存失败：{exception.Message}";
        }
    }

    private async Task DeleteTaskAsync(TaskItemViewModel task)
    {
        try
        {
            await _taskRepository.SoftDeleteTaskAsync(task.Id, DateTimeOffset.UtcNow);
        }
        catch (Exception exception) when (exception is PersistenceException or Microsoft.Data.Sqlite.SqliteException or IOException)
        {
            StatusMessage = $"任务删除保存失败：{exception.Message}";
        }
    }

    private static TaskProjectRecord ToProjectRecord(ProjectItemViewModel project)
        => new(project.Id, project.Name, project.Description, project.CreatedAt, project.UpdatedAt, project.ServerVersion, null);

    private static TaskItemRecord ToTaskRecord(TaskItemViewModel task)
        => new(
            task.Id,
            task.ProjectId,
            task.ProjectName,
            task.Name,
            task.Description,
            task.IsCompleted,
            task.SortId,
            task.CreatedAt,
            task.UpdatedAt,
            task.StartTime,
            task.EndTime,
            task.AssigneeName,
            task.Urgency,
            task.ServerVersion,
            null);
}
