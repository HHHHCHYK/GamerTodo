using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class TaskItemViewModel : ViewModelBase
{
    private static readonly IBrush PendingCheckBackground = Brushes.Transparent;
    private static readonly IBrush CompletedCheckBackground = Brush.Parse("#FF7A45");
    private static readonly IBrush PendingCheckBorderBrush = Brush.Parse("#F0B392");
    private static readonly IBrush CompletedCheckBorderBrush = Brush.Parse("#FF7A45");
    private static readonly IBrush PendingCheckForeground = Brush.Parse("#8A4E36");
    private static readonly IBrush CompletedCheckForeground = Brush.Parse("#FFFFFB");

    public TaskItemViewModel(string projectId, string projectName, string name, string description)
        : this(Guid.NewGuid().ToString("D"), projectId, projectName, name, description, DateTimeOffset.UtcNow)
    {
    }

    public TaskItemViewModel(string id, string projectId, string projectName, string name, string description, DateTimeOffset createdAt)
    {
        Id = id;
        ProjectId = projectId;
        ProjectName = projectName;
        Name = name;
        Description = description;
        CreatedAt = createdAt;
        Urgency = TaskUrgencyLevel.Medium;
    }

    public string Id { get; }

    public string ProjectId { get; }

    public string ProjectName { get; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    public long ServerVersion { get; set; }

    public TextDecorationCollection? TitleTextDecorations => IsCompleted ? TextDecorations.Strikethrough : null;

    public double CardOpacity => IsCompleted ? 0.62 : 1;

    public string CompletionMark => IsCompleted ? "✓" : string.Empty;

    public string UrgencyName => Urgency switch
    {
        TaskUrgencyLevel.Low => "低",
        TaskUrgencyLevel.Medium => "中",
        TaskUrgencyLevel.High => "高",
        TaskUrgencyLevel.Urgent => "紧急",
        _ => "中"
    };

    public string StartTimeText => StartTime?.ToString("yyyy-MM-dd HH:mm") ?? "未设置";

    public string EndTimeText => EndTime?.ToString("yyyy-MM-dd HH:mm") ?? "未设置";

    public string AssigneeNameText => string.IsNullOrWhiteSpace(AssigneeName) ? "未设置" : AssigneeName;

    public IBrush CheckBackground => IsCompleted ? CompletedCheckBackground : PendingCheckBackground;

    public IBrush CheckBorderBrush => IsCompleted ? CompletedCheckBorderBrush : PendingCheckBorderBrush;

    public IBrush CheckForeground => IsCompleted ? CompletedCheckForeground : PendingCheckForeground;

    [ObservableProperty]
    private string _name;

    [ObservableProperty]
    private string _description;

    [ObservableProperty]
    private bool _isCompleted;

    [ObservableProperty]
    private int _sortId;

    [ObservableProperty]
    private DateTimeOffset? _startTime;

    [ObservableProperty]
    private DateTimeOffset? _endTime;

    [ObservableProperty]
    private string _assigneeName = string.Empty;

    [ObservableProperty]
    private TaskUrgencyLevel _urgency;

    partial void OnIsCompletedChanged(bool value)
    {
        OnPropertyChanged(nameof(TitleTextDecorations));
        OnPropertyChanged(nameof(CardOpacity));
        OnPropertyChanged(nameof(CompletionMark));
        OnPropertyChanged(nameof(CheckBackground));
        OnPropertyChanged(nameof(CheckBorderBrush));
        OnPropertyChanged(nameof(CheckForeground));
    }

    partial void OnStartTimeChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(StartTimeText));
    }

    partial void OnEndTimeChanged(DateTimeOffset? value)
    {
        OnPropertyChanged(nameof(EndTimeText));
    }

    partial void OnAssigneeNameChanged(string value)
    {
        OnPropertyChanged(nameof(AssigneeNameText));
    }

    partial void OnUrgencyChanged(TaskUrgencyLevel value)
    {
        OnPropertyChanged(nameof(UrgencyName));
    }
}
