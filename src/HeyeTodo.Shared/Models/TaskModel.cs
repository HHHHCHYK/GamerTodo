using System;
using System.Collections.Generic;

namespace HeyeTodo.Shared.Models;

public sealed class TaskModel
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string ProjectId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskState State { get; set; } = TaskState.Backlog;
    public TaskPriorityLevel Priority { get; set; } = TaskPriorityLevel.Normal;
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}
