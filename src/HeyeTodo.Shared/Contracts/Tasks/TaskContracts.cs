using HeyeTodo.Shared.Enums;
using HeyeTodo.Shared.Sync;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Contracts.Tasks;

public sealed record ProjectDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    SyncMeta Sync);

public sealed record CreateProjectRequest(
    string Name,
    string? Description);

public sealed record UpdateProjectRequest(
    Guid Id,
    string Name,
    string? Description);

public sealed record TaskDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    double? EstimatedHours,
    Guid? AssigneeId,
    IReadOnlyDictionary<string, object?>? RoleFields,
    SyncMeta Sync);

public sealed record TaskDependencyDto(
    Guid Id,
    Guid ProjectId,
    Guid PredecessorId,
    Guid SuccessorId,
    DependencyType Type,
    SyncMeta Sync);

public sealed record CreateTaskRequest
{
    public Guid ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TaskStatus Status { get; init; } = TaskStatus.Backlog;
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public double? EstimatedHours { get; init; }
    public Guid? AssigneeId { get; init; }
    public string? RoleFieldsJson { get; init; }
}

public sealed record UpdateTaskRequest
{
    public Guid Id { get; init; }
    public Guid ProjectId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public TaskStatus Status { get; init; } = TaskStatus.Backlog;
    public TaskPriority Priority { get; init; } = TaskPriority.Normal;
    public DateTimeOffset? StartDate { get; init; }
    public DateTimeOffset? EndDate { get; init; }
    public double? EstimatedHours { get; init; }
    public Guid? AssigneeId { get; init; }
    public string? RoleFieldsJson { get; init; }
}

public sealed record ChangeTaskStatusRequest
{
    public Guid Id { get; init; }
    public TaskStatus Status { get; init; } = TaskStatus.Backlog;
}

public sealed record TaskListQuery
{
    public Guid? ProjectId { get; init; }
    public TaskStatus? Status { get; init; }
    public TaskPriority? Priority { get; init; }
    public bool IncludeCompleted { get; init; }
    public string? Search { get; init; }
    public TaskSortField SortBy { get; init; } = TaskSortField.UpdatedAt;
    public SortDirection SortDirection { get; init; } = SortDirection.Descending;
}
