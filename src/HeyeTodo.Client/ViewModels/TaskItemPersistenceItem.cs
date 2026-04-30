namespace HeyeTodo.Client.ViewModels;

public sealed class TaskItemPersistenceItem
{
    public string Id { get; set; } = string.Empty;

    public string ProjectId { get; set; } = string.Empty;

    public string ProjectNameSnapshot { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsCompleted { get; set; }

    public int SortId { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? StartTime { get; set; }

    public DateTimeOffset? EndTime { get; set; }

    public string AssigneeName { get; set; } = string.Empty;

    public TaskUrgencyLevel Urgency { get; set; } = TaskUrgencyLevel.Medium;
}
