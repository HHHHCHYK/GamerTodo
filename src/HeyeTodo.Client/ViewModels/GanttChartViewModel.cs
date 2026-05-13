using System;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class GanttChartViewModel : ViewModelBase
{
    private const double TaskNameColumnWidth = 180;
    private const double DayColumnWidth = 112;
    private const double RowHeightValue = 54;
    private const double BarHeight = 30;
    private readonly TaskPanelViewModel _taskPanel;

    public GanttChartViewModel(TaskPanelViewModel taskPanel)
    {
        _taskPanel = taskPanel;
        WeekStart = ResolveWeekStart(DateTime.Today);
        _taskPanel.Tasks.CollectionChanged += OnTasksCollectionChanged;
        foreach (var task in _taskPanel.Tasks)
        {
            task.PropertyChanged += OnTaskPropertyChanged;
        }
        RebuildTimeline();
    }

    public ObservableCollection<GanttDayColumnViewModel> Days { get; } = new();

    public ObservableCollection<GanttTaskBarViewModel> Bars { get; } = new();

    public TaskPanelViewModel TaskPanel => _taskPanel;

    public double TaskNameWidth => TaskNameColumnWidth;

    public double TimelineWidth => DayColumnWidth * 7;

    public double DragDayWidth => DayColumnWidth;

    public double ContentHeight => Math.Max(320, Bars.Count * RowHeightValue);

    public string WeekTitle => $"{WeekStart:yyyy年M月d日} — {WeekStart.AddDays(6):M月d日}";

    public bool HasNoTasks => Bars.Count == 0;

    public double TodayLineLeft
    {
        get
        {
            var today = DateTime.Today;
            return today < WeekStart || today > WeekStart.AddDays(6)
                ? -100
                : TaskNameColumnWidth + ((today - WeekStart).TotalDays * DayColumnWidth) + DayColumnWidth / 2;
        }
    }

    public bool IsTodayInCurrentWeek => TodayLineLeft >= 0;

    public DateTime WeekStart { get; private set; }

    [RelayCommand]
    private void PreviousWeek()
    {
        WeekStart = WeekStart.AddDays(-7);
        RebuildTimeline();
    }

    [RelayCommand]
    private void NextWeek()
    {
        WeekStart = WeekStart.AddDays(7);
        RebuildTimeline();
    }

    [RelayCommand]
    private void GoToToday()
    {
        WeekStart = ResolveWeekStart(DateTime.Today);
        RebuildTimeline();
    }

    [RelayCommand]
    private void Refresh()
    {
        RebuildTimeline();
    }

    [RelayCommand]
    private void SelectTask(TaskItemViewModel task)
    {
        _taskPanel.SelectTask(task);
    }

    public void OpenTaskDetails(TaskItemViewModel task)
    {
        _taskPanel.SelectTask(task);
    }

    public void ApplyDrag(GanttTaskBarViewModel bar, int dayDelta, GanttDragMode mode)
    {
        if (dayDelta == 0)
        {
            return;
        }

        var range = ResolveRange(bar.Task);
        var start = range.start;
        var end = range.end;

        switch (mode)
        {
            case GanttDragMode.ResizeStart:
                start = start.AddDays(dayDelta);
                if (start > end)
                {
                    start = end;
                }
                break;
            case GanttDragMode.ResizeEnd:
                end = end.AddDays(dayDelta);
                if (end < start)
                {
                    end = start;
                }
                break;
            default:
                start = start.AddDays(dayDelta);
                end = end.AddDays(dayDelta);
                break;
        }

        _taskPanel.UpdateTaskSchedule(bar.Task, start, end);
        RebuildTimeline();
    }

    public void RebuildTimeline()
    {
        Days.Clear();
        Bars.Clear();

        for (var i = 0; i < 7; i++)
        {
            var day = WeekStart.AddDays(i);
            Days.Add(new GanttDayColumnViewModel(ResolveDayName(day), day.ToString("M/d", CultureInfo.CurrentCulture), day.Date == DateTime.Today, i * DayColumnWidth));
        }

        var weekEnd = WeekStart.AddDays(6);
        var tasks = _taskPanel.Tasks
            .OrderBy(task => ResolveRange(task).start)
            .ThenBy(task => task.SortId)
            .ThenBy(task => task.CreatedAt)
            .ToList();

        var bars = tasks
            .Select((task, index) => BuildBar(task, index, weekEnd))
            .Where(bar => bar is not null)
            .Cast<GanttTaskBarViewModel>()
            .ToList();

        ApplyConflictState(bars);

        foreach (var bar in bars)
        {
            Bars.Add(bar);
        }

        OnPropertyChanged(nameof(WeekTitle));
        OnPropertyChanged(nameof(ContentHeight));
        OnPropertyChanged(nameof(HasNoTasks));
        OnPropertyChanged(nameof(TodayLineLeft));
        OnPropertyChanged(nameof(IsTodayInCurrentWeek));
    }

    private GanttTaskBarViewModel? BuildBar(TaskItemViewModel task, int index, DateTime weekEnd)
    {
        var range = ResolveRange(task);
        var visibleStart = range.start < WeekStart ? WeekStart : range.start;
        var visibleEnd = range.end > weekEnd ? weekEnd : range.end;

        if (range.end < WeekStart || range.start > weekEnd)
        {
            return null;
        }

        var leftDays = (visibleStart - WeekStart).TotalDays;
        var widthDays = Math.Max(1, (visibleEnd - visibleStart).TotalDays + 1);
        var top = index * RowHeightValue + (RowHeightValue - BarHeight) / 2;

        return new GanttTaskBarViewModel(task)
        {
            Left = TaskNameColumnWidth + leftDays * DayColumnWidth + 8,
            Width = widthDays * DayColumnWidth - 16,
            Top = top,
            RowTop = index * RowHeightValue,
            RowHeight = RowHeightValue,
            IsPlaceholder = range.isPlaceholder,
            IsContinuesBefore = range.start < WeekStart,
            IsContinuesAfter = range.end > weekEnd
        };
    }

    private void OnTasksCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems is not null)
        {
            foreach (TaskItemViewModel task in e.OldItems)
            {
                task.PropertyChanged -= OnTaskPropertyChanged;
            }
        }

        if (e.NewItems is not null)
        {
            foreach (TaskItemViewModel task in e.NewItems)
            {
                task.PropertyChanged += OnTaskPropertyChanged;
            }
        }

        RebuildTimeline();
    }

    private void OnTaskPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(TaskItemViewModel.StartTime)
            or nameof(TaskItemViewModel.EndTime)
            or nameof(TaskItemViewModel.IsCompleted)
            or nameof(TaskItemViewModel.Name)
            or nameof(TaskItemViewModel.AssigneeName)
            or nameof(TaskItemViewModel.Urgency)
            or nameof(TaskItemViewModel.SortId))
        {
            RebuildTimeline();
        }
    }

    private static void ApplyConflictState(System.Collections.Generic.IReadOnlyList<GanttTaskBarViewModel> bars)
    {
        for (var i = 0; i < bars.Count; i++)
        {
            var first = ResolveRange(bars[i].Task);

            for (var j = i + 1; j < bars.Count; j++)
            {
                var second = ResolveRange(bars[j].Task);
                if (first.start <= second.end && second.start <= first.end)
                {
                    bars[i].IsConflict = true;
                    bars[j].IsConflict = true;
                }
            }
        }
    }

    private static (DateTime start, DateTime end, bool isPlaceholder) ResolveRange(TaskItemViewModel task)
    {
        var start = task.StartTime?.LocalDateTime.Date;
        var end = task.EndTime?.LocalDateTime.Date;

        if (start is not null && end is not null)
        {
            return start <= end ? (start.Value, end.Value, false) : (end.Value, start.Value, false);
        }

        if (start is not null)
        {
            return (start.Value, start.Value, true);
        }

        if (end is not null)
        {
            return (end.Value, end.Value, true);
        }

        return (DateTime.Today, DateTime.Today, true);
    }

    private static DateTime ResolveWeekStart(DateTime date)
    {
        var diff = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.Date.AddDays(-diff);
    }

    private static string ResolveDayName(DateTime day)
    {
        return day.DayOfWeek switch
        {
            DayOfWeek.Monday => "周一",
            DayOfWeek.Tuesday => "周二",
            DayOfWeek.Wednesday => "周三",
            DayOfWeek.Thursday => "周四",
            DayOfWeek.Friday => "周五",
            DayOfWeek.Saturday => "周六",
            DayOfWeek.Sunday => "周日",
            _ => string.Empty
        };
    }
}
