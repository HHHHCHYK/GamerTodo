using System;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class GanttTaskBarViewModel : ViewModelBase
{
    public GanttTaskBarViewModel(TaskItemViewModel task)
    {
        Task = task;
    }

    public TaskItemViewModel Task { get; }

    public string Name => Task.Name;

    public string TooltipTitle => Task.Name;

    public string TooltipTimeRange => $"{ResolveDateText(Task.StartTime)} → {ResolveDateText(Task.EndTime)}";

    public string TooltipAssignee => $"执行人：{Task.AssigneeNameText}";

    public string TooltipUrgency => $"紧急程度：{Task.UrgencyName}";

    public string DisplayText => IsContinuesAfter ? $"{Task.Name} →" : Task.Name;

    public string LeftMarker => IsContinuesBefore ? "←" : string.Empty;

    public double Left { get; set; }

    public double Width { get; set; }

    public double Top { get; set; }

    public double RowTop { get; set; }

    public double RowHeight { get; set; }

    public bool IsPlaceholder { get; set; }

    public bool IsConflict { get; set; }

    public bool IsContinuesBefore { get; set; }

    public bool IsContinuesAfter { get; set; }

    public double Opacity => Task.IsCompleted ? 0.42 : 1;

    public string Background => IsConflict ? "#D94A38" : IsPlaceholder ? "#66FFB37A" : "#FF8A4C";

    public string BorderBrush => IsConflict ? "#A62F21" : IsPlaceholder ? "#FFB37A" : "#FF7A45";

    public string Foreground => IsPlaceholder ? "#8A4E36" : "#FFFFFB";

    public string BorderDashArray => IsPlaceholder ? "4 3" : string.Empty;

    public bool HasLeftMarker => !string.IsNullOrWhiteSpace(LeftMarker);

    private static string ResolveDateText(DateTimeOffset? value)
    {
        return value?.ToString("M月d日") ?? "未设置";
    }
}
