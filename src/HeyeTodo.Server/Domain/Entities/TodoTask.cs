using System;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Server.Domain.Entities;

/// <summary>
/// Core task entity. Named <c>TodoTask</c> to avoid clashing with <see cref="System.Threading.Tasks.Task"/>.
/// </summary>
public sealed class TodoTask : SyncableEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public string Title { get; set; } = null!;
    public string? Description { get; set; }

    public TaskStatus Status { get; set; } = TaskStatus.Backlog;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;

    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public double? EstimatedHours { get; set; }

    public Guid? AssigneeId { get; set; }
    public AppUser? Assignee { get; set; }

    /// <summary>JSON blob with role-specific fields (jsonb column in Postgres).</summary>
    public string? RoleFieldsJson { get; set; }
}
