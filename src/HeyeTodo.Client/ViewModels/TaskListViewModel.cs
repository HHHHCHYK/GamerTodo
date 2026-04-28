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
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class TaskListViewModel : ViewModelBase
{
    // 这个 ViewModel 负责“任务列表页面”的状态和操作。
    // View 只负责展示，真正的加载、筛选、保存、删除等逻辑都集中在这里。
    private readonly ITaskWorkspaceService _workspace;
    private readonly ISyncCoordinator _sync;
    private readonly IClientLogger _logger;
    private readonly ClientSession _session;
    private readonly Guid _clientId;

    // 刷新列表时会临时设置这个标记，避免 SelectedProject/SelectedTask 的变化再次触发重复加载。
    private bool _reloadingSelection;

    // ObservableCollection 会在集合变化时通知 Avalonia UI 自动刷新列表控件。
    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<TaskItemViewModel> Tasks { get; } = new();
    public ObservableCollection<TaskStatusOption> StatusFilters { get; } = new();
    public ObservableCollection<TaskPriorityOption> PriorityFilters { get; } = new();
    public ObservableCollection<TaskSortOption> SortOptions { get; } = new();
    public ObservableCollection<TaskStatusOption> EditorStatuses { get; } = new();
    public ObservableCollection<TaskPriorityOption> EditorPriorities { get; } = new();

    // CommunityToolkit.Mvvm 会根据 [ObservableProperty] 自动生成公开属性和变更通知。
    // 例如 _selectedProject 会生成 SelectedProject，并在赋值时触发 OnSelectedProjectChanged。
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

    // 这些只读属性把内部状态转换成 UI 可直接绑定的布尔值。
    // 当相关状态变化时，需要手动调用 OnPropertyChanged 通知界面重新计算。
    public bool HasProjects => Projects.Count > 0;
    public bool HasTasks => Tasks.Count > 0;
    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasSelectedTask => SelectedTask is not null;
    public bool ShowEditorForm => IsEditorVisible && HasSelectedProject;
    public bool ShowNoProjectMessage => !HasSelectedProject;
    public bool ShowEmptyTasksMessage => HasSelectedProject && !HasTasks;
    public bool ShowTaskList => HasSelectedProject && HasTasks;

    public TaskListViewModel(ITaskWorkspaceService workspace, ISyncCoordinator sync, IClientLogger logger)
    {
        _workspace = workspace;
        _sync = sync;
        _logger = logger;
        _session = AppHost.Services.GetRequiredService<ClientSession>();
        _clientId = AppPaths.GetOrCreateClientId();

        // 同步器发现当前项目数据失效时，会回调这里重新加载任务列表。
        _sync.ProjectInvalidated += OnProjectInvalidated;

        // 过滤器和排序项使用本地化 key，而不是直接写中文/英文文本，方便界面多语言切换。
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

        // 编辑器不需要“全部”选项，所以从过滤器集合中复制非空状态和优先级。
        foreach (var item in PriorityFilters.Where(x => x.Value is not null))
        {
            EditorPriorities.Add(item);
        }

        SelectedStatusFilter = StatusFilters[0];
        SelectedPriorityFilter = PriorityFilters[0];
        SelectedSort = SortOptions[0];
        EditStatus = EditorStatuses.FirstOrDefault(x => x.Value == TaskStatus.Todo);
        EditPriority = EditorPriorities.FirstOrDefault(x => x.Value == TaskPriority.Normal);

        // 构造完成后立即异步加载项目列表，界面打开时就能看到数据。
        _ = LoadAsync();
    }

    partial void OnSelectedProjectChanged(ProjectItemViewModel? value)
    {
        var previousProjectId = SelectedProject?.Id;

        // 选中项目变化后，多个 UI 显示状态都依赖它，需要一起通知刷新。
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(ShowEditorForm));
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
            // 没有项目被选中时，清空任务列表和编辑器，避免界面显示旧项目的数据。
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

        // 只订阅当前项目的同步更新，减少无关项目变更对当前页面的影响。
        _ = _sync.SubscribeProjectAsync(value.Id);
        ResetEditor();
        _ = LoadTasksAsync();
    }

    partial void OnSelectedTaskChanged(TaskItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedTask));
        OnPropertyChanged(nameof(ShowEditorForm));
        if (_reloadingSelection)
        {
            return;
        }

        // 选中任务后，把只读列表项的数据复制到编辑表单字段中。
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
    partial void OnIsEditorVisibleChanged(bool value) => OnPropertyChanged(nameof(ShowEditorForm));

    // [RelayCommand] 会自动生成可供按钮绑定的 LoadCommand、RefreshCommand 等命令属性。
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

            // 项目列表来自本地优先的工作区服务，服务内部再决定是否需要同步到服务器。
            var projects = await _workspace.ListProjectsAsync(_session.UserId.Value);
            Projects.ReplaceWith(projects.Select(x => new ProjectItemViewModel(x)));
            OnPropertyChanged(nameof(HasProjects));

            // 重新加载项目后尽量保留原来的选择，避免刷新时用户上下文丢失。
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("LoadTaskWorkspace", ex, BuildLogProperties());
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.LoadFailed"]} {ex.Message}";
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
        // SignalR/同步流程通知某个项目有新变化时，只刷新当前正在查看的项目。
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
            // 创建项目时传入 clientId，用于本地优先同步体系识别这次变更来自哪个客户端。
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

            // Synced=false 说明操作已先保存到本地，之后会通过同步队列再上传。
            StatusMessage = project.Synced
                ? LocalizationService.Instance["Tasks.ProjectCreated"]
                : LocalizationService.Instance["Tasks.Warning.LocalOnly"];
        }
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("CreateProject", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["projectName"] = name,
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.ProjectCreateFailed"]} {ex.Message}";
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("UpdateProject", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["projectId"] = SelectedProject?.Id,
                ["projectName"] = name,
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.ProjectUpdateFailed"]} {ex.Message}";
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("DeleteProject", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["projectId"] = deletedId,
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.ProjectDeleteFailed"]} {ex.Message}";
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

        // 新建任务时先取消列表中的任务选中状态，但不触发编辑器重置。
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
        // 如果正在编辑已有任务，取消时恢复为该任务原本的字段值。
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
            // IsCreateMode 决定当前表单是创建新任务，还是更新列表中选中的任务。
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync(IsCreateMode || SelectedTask is null ? "CreateTask" : "UpdateTask", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["projectId"] = SelectedProject.Id,
                ["taskId"] = SelectedTask?.Id,
                ["taskTitle"] = EditTitle.Trim(),
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.TaskSaveFailed"]} {ex.Message}";
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("DeleteTask", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["taskId"] = deletedId,
                ["projectId"] = SelectedProject?.Id,
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.TaskDeleteFailed"]} {ex.Message}";
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("AdvanceTaskStatus", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["taskId"] = task.Id,
                ["projectId"] = SelectedProject?.Id,
                ["fromStatus"] = task.Status.ToString(),
                ["toStatus"] = GetNextStatus(task.Status).ToString(),
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.StatusUpdateFailed"]} {ex.Message}";
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

        try
        {
            // TaskListQuery 把 UI 上的筛选、搜索和排序状态打包成应用层查询对象。
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

            // 刷新任务集合后优先选中指定任务，其次尝试保持刷新前的任务选中状态。
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
        catch (Exception ex)
        {
            await _logger.LogUserOperationExceptionAsync("LoadTasks", ex, BuildLogProperties(new Dictionary<string, object?>
            {
                ["projectId"] = SelectedProject.Id,
                ["selectTaskId"] = selectTaskId,
                ["statusFilter"] = SelectedStatusFilter?.Value?.ToString(),
                ["priorityFilter"] = SelectedPriorityFilter?.Value?.ToString(),
                ["hasSearchText"] = !string.IsNullOrWhiteSpace(SearchText),
            }));
            ErrorMessage = $"{LocalizationService.Instance["Tasks.Error.LoadFailed"]} {ex.Message}";
        }
    }

    private void ResetEditor()
    {
        // 统一清空编辑器状态，避免多个命令里重复写同一组字段重置逻辑。
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
        // 使用 out 参数一次返回开始和结束日期，失败时通过 ErrorMessage 告诉界面显示错误。
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

        // 预估工时是可选字段，空字符串表示未填写。
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
        // 日期字段也是可选的，解析失败才返回 false。
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

    // 把用户输入统一整理为 null 或去除首尾空格后的字符串，方便服务层保存。
    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private IReadOnlyDictionary<string, object?> BuildLogProperties(IReadOnlyDictionary<string, object?>? properties = null)
    {
        var result = new Dictionary<string, object?>
        {
            ["userId"] = _session.UserId,
            ["activeProjectId"] = SelectedProject?.Id,
            ["selectedTaskId"] = SelectedTask?.Id,
        };

        if (properties is not null)
        {
            foreach (var item in properties)
            {
                result[item.Key] = item.Value;
            }
        }

        return result;
    }

    // 快捷推进任务状态时使用的状态流转规则。
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
    // 列表项 ViewModel 会把本地实体转换成界面更容易绑定的只读数据。
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
    // 任务列表项保留任务核心字段，同时额外提供本地化后的显示文本。
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

        // 根据开始/结束日期是否存在，生成列表中显示的日程文本。
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
    // 下拉框选项同时包含枚举值和本地化 key，UI 显示文本由 OptionItem 处理。
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
    // 优先级选项的结构和状态选项相同，只是枚举类型不同。
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
    // 排序选项由“排序字段”和“排序方向”组成，例如按更新时间倒序。
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
    // 用新数据替换整个 ObservableCollection，让绑定到集合的控件收到清空和新增通知。
    public static void ReplaceWith<T>(this ObservableCollection<T> collection, IEnumerable<T> items)
    {
        collection.Clear();
        foreach (var item in items)
        {
            collection.Add(item);
        }
    }
}
