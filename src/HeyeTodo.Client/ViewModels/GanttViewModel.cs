using System;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Application.Tasks;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Shared.Enums;
using Microsoft.Extensions.DependencyInjection;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class GanttViewModel : ViewModelBase
{
    private const double DefaultDayWidth = 34;
    private const double MinDayWidth = 18;
    private const double MaxDayWidth = 72;
    private const double RowHeightValue = 44;
    private const double HeaderHeightValue = 48;
    private const double BarHeightValue = 22;

    private readonly ITaskWorkspaceService _workspace;
    private readonly ISyncCoordinator _sync;
    private readonly ClientSession _session;
    private readonly Guid _clientId;
    private bool _reloadingSelection;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<GanttTaskItemViewModel> Tasks { get; } = new();
    public ObservableCollection<GanttDependencyLineViewModel> Dependencies { get; } = new();
    public ObservableCollection<GanttDayHeaderViewModel> DayHeaders { get; } = new();

    [ObservableProperty] private ProjectItemViewModel? _selectedProject;
    [ObservableProperty] private GanttTaskItemViewModel? _selectedTask;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private string? _statusMessage;
    [ObservableProperty] private DateTimeOffset _timelineStart = DateTimeOffset.UtcNow.Date;
    [ObservableProperty] private DateTimeOffset _timelineEnd = DateTimeOffset.UtcNow.Date.AddDays(14);
    [ObservableProperty] private double _dayWidth = DefaultDayWidth;
    [ObservableProperty] private double _canvasWidth;
    [ObservableProperty] private double _canvasHeight;
    [ObservableProperty] private string _timelineLabel = string.Empty;

    public double RowHeight => RowHeightValue;
    public double HeaderHeight => HeaderHeightValue;
    public double BarHeight => BarHeightValue;
    public bool HasProjects => Projects.Count > 0;
    public bool HasSelectedProject => SelectedProject is not null;
    public bool HasTasks => Tasks.Count > 0;
    public bool ShowNoProjectMessage => !HasSelectedProject;
    public bool ShowEmptyTasksMessage => HasSelectedProject && !HasTasks;
    public bool ShowGantt => HasSelectedProject && HasTasks;

    public GanttViewModel(ITaskWorkspaceService workspace, ISyncCoordinator sync)
    {
        _workspace = workspace;
        _sync = sync;
        _session = AppHost.Services.GetRequiredService<ClientSession>();
        _clientId = AppPaths.GetOrCreateClientId();
        _sync.ProjectInvalidated += OnProjectInvalidated;
        _ = LoadAsync();
    }

    partial void OnSelectedProjectChanged(ProjectItemViewModel? value)
    {
        OnPropertyChanged(nameof(HasSelectedProject));
        OnPropertyChanged(nameof(ShowNoProjectMessage));
        OnPropertyChanged(nameof(ShowEmptyTasksMessage));
        OnPropertyChanged(nameof(ShowGantt));

        if (_reloadingSelection)
        {
            return;
        }

        SelectedTask = null;
        if (value is null)
        {
            Tasks.Clear();
            Dependencies.Clear();
            DayHeaders.Clear();
            OnPropertyChanged(nameof(HasTasks));
            return;
        }

        _ = _sync.SubscribeProjectAsync(value.Id);
        _ = LoadGanttAsync();
    }

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
            var currentProjectId = SelectedProject?.Id;
            var projects = await _workspace.ListProjectsAsync(_session.UserId.Value);
            Projects.ReplaceWith(projects.Select(x => new ProjectItemViewModel(x)));
            OnPropertyChanged(nameof(HasProjects));

            _reloadingSelection = true;
            SelectedProject = currentProjectId is not null
                ? Projects.FirstOrDefault(x => x.Id == currentProjectId.Value)
                : Projects.FirstOrDefault();
            _reloadingSelection = false;

            if (SelectedProject is null)
            {
                Tasks.Clear();
                Dependencies.Clear();
                DayHeaders.Clear();
                OnPropertyChanged(nameof(HasTasks));
                OnPropertyChanged(nameof(ShowEmptyTasksMessage));
                OnPropertyChanged(nameof(ShowGantt));
                return;
            }

            await _sync.SubscribeProjectAsync(SelectedProject.Id);
            await LoadGanttAsync();
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
    private Task RefreshAsync() => LoadAsync();

    [RelayCommand]
    private void ZoomIn()
    {
        DayWidth = Math.Min(MaxDayWidth, DayWidth + 8);
        RecalculateLayout();
    }

    [RelayCommand]
    private void ZoomOut()
    {
        DayWidth = Math.Max(MinDayWidth, DayWidth - 8);
        RecalculateLayout();
    }

    [RelayCommand]
    private void Today()
    {
        var today = DateTimeOffset.Now.Date;
        TimelineStart = today.AddDays(-3);
        TimelineEnd = today.AddDays(18);
        RecalculateLayout();
    }

    public async Task RescheduleTaskAsync(Guid taskId, double startDeltaPixels, double endDeltaPixels)
    {
        if (_session.UserId is null)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.NoSession"];
            return;
        }

        var task = Tasks.FirstOrDefault(x => x.Id == taskId);
        if (task is null)
        {
            return;
        }

        var startDeltaDays = PixelsToDays(startDeltaPixels);
        var endDeltaDays = PixelsToDays(endDeltaPixels);
        if (startDeltaDays == 0 && endDeltaDays == 0)
        {
            return;
        }

        var start = task.StartDate.Date.AddDays(startDeltaDays);
        var end = task.EndDate.Date.AddDays(endDeltaDays);
        if (end < start)
        {
            end = start;
        }

        IsBusy = true;
        ErrorMessage = null;
        StatusMessage = null;
        try
        {
            var updated = await _workspace.RescheduleTaskAsync(_session.UserId.Value, taskId, start, end, _clientId);
            if (updated is null)
            {
                ErrorMessage = LocalizationService.Instance["Tasks.Error.TaskSaveFailed"];
                return;
            }

            await LoadGanttAsync(taskId);
            if (!updated.Synced)
            {
                StatusMessage = LocalizationService.Instance["Tasks.Warning.LocalOnly"];
            }
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

    private async Task LoadGanttAsync(Guid? selectedTaskId = null)
    {
        if (_session.UserId is null || SelectedProject is null)
        {
            return;
        }

        var snapshot = await _workspace.GetGanttSnapshotAsync(_session.UserId.Value, SelectedProject.Id);
        var scheduledTasks = snapshot.Tasks
            .Where(x => x.StartDate is not null || x.EndDate is not null)
            .Select(NormalizeTaskDates)
            .OrderBy(x => x.StartDate)
            .ThenBy(x => x.EndDate)
            .ThenBy(x => x.Title)
            .ToList();

        BuildTimeline(scheduledTasks);
        Tasks.ReplaceWith(scheduledTasks.Select((task, index) => new GanttTaskItemViewModel(task, index, TimelineStart, DayWidth, RowHeight, BarHeight)));
        Dependencies.ReplaceWith(BuildDependencies(snapshot.Dependencies));

        SelectedTask = selectedTaskId is not null
            ? Tasks.FirstOrDefault(x => x.Id == selectedTaskId.Value)
            : Tasks.FirstOrDefault(x => SelectedTask is not null && x.Id == SelectedTask.Id);

        OnPropertyChanged(nameof(HasTasks));
        OnPropertyChanged(nameof(ShowEmptyTasksMessage));
        OnPropertyChanged(nameof(ShowGantt));
    }

    private void BuildTimeline(IReadOnlyList<LocalTask> tasks)
    {
        var today = DateTimeOffset.Now.Date;
        var min = tasks.Count == 0
            ? today.AddDays(-3)
            : tasks.Min(x => x.StartDate ?? x.EndDate ?? today).Date.AddDays(-3);
        var max = tasks.Count == 0
            ? today.AddDays(18)
            : tasks.Max(x => x.EndDate ?? x.StartDate ?? today).Date.AddDays(3);

        TimelineStart = min;
        TimelineEnd = max < min.AddDays(7) ? min.AddDays(7) : max;
        RecalculateLayout();
    }

    private void RecalculateLayout()
    {
        var days = Math.Max(1, (TimelineEnd.Date - TimelineStart.Date).Days + 1);
        CanvasWidth = days * DayWidth;
        CanvasHeight = HeaderHeight + Math.Max(1, Tasks.Count) * RowHeight;
        TimelineLabel = $"{TimelineStart:yyyy-MM-dd} - {TimelineEnd:yyyy-MM-dd}";

        DayHeaders.ReplaceWith(Enumerable.Range(0, days).Select(i => new GanttDayHeaderViewModel(
            TimelineStart.AddDays(i),
            i * DayWidth,
            DayWidth,
            HeaderHeight)));

        foreach (var task in Tasks)
        {
            task.UpdateLayout(TimelineStart, DayWidth, RowHeight, BarHeight);
        }

        RebuildDependencies();
    }

    private void RebuildDependencies()
    {
        var source = Dependencies.Select(x => x.Source).ToList();
        Dependencies.ReplaceWith(BuildDependencies(source));
    }

    private IReadOnlyList<GanttDependencyLineViewModel> BuildDependencies(IReadOnlyList<LocalDependency> dependencies)
    {
        var byId = Tasks.ToDictionary(x => x.Id);
        var lines = new List<GanttDependencyLineViewModel>();
        foreach (var dependency in dependencies)
        {
            if (!byId.TryGetValue(dependency.PredecessorId, out var predecessor)
                || !byId.TryGetValue(dependency.SuccessorId, out var successor))
            {
                continue;
            }

            lines.Add(new GanttDependencyLineViewModel(dependency, predecessor, successor));
        }

        return lines;
    }

    private int PixelsToDays(double pixels)
    {
        if (Math.Abs(pixels) < DayWidth / 2)
        {
            return 0;
        }

        return (int)Math.Round(pixels / DayWidth, MidpointRounding.AwayFromZero);
    }

    private void OnProjectInvalidated(Guid projectId)
    {
        if (SelectedProject?.Id == projectId)
        {
            _ = LoadGanttAsync(SelectedTask?.Id);
        }
    }

    private static LocalTask NormalizeTaskDates(LocalTask task)
    {
        var start = task.StartDate ?? task.EndDate ?? DateTimeOffset.Now.Date;
        var end = task.EndDate ?? task.StartDate ?? start;
        if (end < start)
        {
            end = start;
        }

        task.StartDate = start.Date;
        task.EndDate = end.Date;
        return task;
    }
}

public sealed partial class GanttTaskItemViewModel : ObservableObject
{
    public Guid Id { get; }
    public string Title { get; }
    public string? Description { get; }
    public TaskStatus Status { get; }
    public TaskPriority Priority { get; }
    public DateTimeOffset StartDate { get; }
    public DateTimeOffset EndDate { get; }
    public double EstimatedDays { get; }
    public string DateLabel => $"{StartDate:MM-dd} → {EndDate:MM-dd}";
    public string StatusLabel => LocalizationService.Instance[TaskStatusOption.KeyFor(Status)];
    public string PriorityLabel => LocalizationService.Instance[TaskPriorityOption.KeyFor(Priority)];

    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;
    [ObservableProperty] private double _width;
    [ObservableProperty] private double _height;

    public GanttTaskItemViewModel(LocalTask task, int rowIndex, DateTimeOffset timelineStart, double dayWidth, double rowHeight, double barHeight)
    {
        Id = task.Id;
        Title = task.Title;
        Description = task.Description;
        Status = task.Status;
        Priority = task.Priority;
        StartDate = (task.StartDate ?? task.EndDate ?? DateTimeOffset.Now.Date).Date;
        EndDate = (task.EndDate ?? task.StartDate ?? StartDate).Date;
        EstimatedDays = Math.Max(1, (EndDate.Date - StartDate.Date).Days + 1);
        UpdateLayout(timelineStart, dayWidth, rowHeight, barHeight, rowIndex);
    }

    public void UpdateLayout(DateTimeOffset timelineStart, double dayWidth, double rowHeight, double barHeight, int? rowIndex = null)
    {
        var row = rowIndex ?? (int)Math.Round((Y - 48) / rowHeight);
        X = Math.Max(0, (StartDate.Date - timelineStart.Date).Days * dayWidth);
        Y = 48 + row * rowHeight + (rowHeight - barHeight) / 2;
        Width = Math.Max(dayWidth, EstimatedDays * dayWidth);
        Height = barHeight;
    }
}

public sealed class GanttDependencyLineViewModel
{
    public LocalDependency Source { get; }
    public double StartX { get; }
    public double StartY { get; }
    public double EndX { get; }
    public Point StartPoint { get; }
    public Point EndPoint { get; }
    public AvaloniaList<Point> ArrowPoints { get; } = new();

    public GanttDependencyLineViewModel(LocalDependency source, GanttTaskItemViewModel predecessor, GanttTaskItemViewModel successor)
    {
        Source = source;
        var predecessorLeft = predecessor.X;
        var predecessorRight = predecessor.X + predecessor.Width;
        var successorLeft = successor.X;
        var successorRight = successor.X + successor.Width;
        var predecessorY = predecessor.Y + predecessor.Height / 2;
        var successorY = successor.Y + successor.Height / 2;

        double endX;
        (StartX, endX) = source.Type switch
        {
            DependencyType.StartToStart => (predecessorLeft, successorLeft),
            DependencyType.FinishToFinish => (predecessorRight, successorRight),
            DependencyType.StartToFinish => (predecessorLeft, successorRight),
            _ => (predecessorRight, successorLeft),
        };

        StartY = predecessorY;
        var endY = successorY;
        EndX = endX;
        StartPoint = new Point(StartX, StartY);
        EndPoint = new Point(endX, endY);
        var arrowX = endX >= StartX ? endX - 7 : endX + 7;
        var arrowPoint1 = new Point(endX, endY);
        var arrowPoint2 = new Point(arrowX, endY - 5);
        var arrowPoint3 = new Point(arrowX, endY + 5);
        ArrowPoints.Add(arrowPoint1);
        ArrowPoints.Add(arrowPoint2);
        ArrowPoints.Add(arrowPoint3);
    }
}

public sealed class GanttDayHeaderViewModel
{
    public DateTimeOffset Date { get; }
    public double X { get; }
    public double Width { get; }
    public double Height { get; }
    public string DayLabel => Date.ToString("dd", CultureInfo.InvariantCulture);
    public string MonthLabel => Date.Day == 1 ? Date.ToString("MMM", CultureInfo.InvariantCulture) : string.Empty;
    public bool IsToday => Date.Date == DateTimeOffset.Now.Date;

    public GanttDayHeaderViewModel(DateTimeOffset date, double x, double width, double height)
    {
        Date = date;
        X = x;
        Width = width;
        Height = height;
    }
}
