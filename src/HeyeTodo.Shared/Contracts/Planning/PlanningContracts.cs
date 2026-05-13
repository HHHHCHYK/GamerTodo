using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Contracts.Planning;

public sealed record PlanningRequest
{
    public Guid? ProjectId { get; init; }
    public string Mode { get; init; } = "rule";
    public string? Prompt { get; init; }
    public DateTimeOffset? AnchorDate { get; init; }
    public IReadOnlyList<PlanningTask> Tasks { get; init; } = [];
    public IReadOnlyList<PlanningDependency> Dependencies { get; init; } = [];
}

public sealed record PlanningTask(
    Guid Id,
    Guid ProjectId,
    string Title,
    TaskStatus Status,
    TaskPriority Priority,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    double? EstimatedHours);

public sealed record PlanningDependency(
    Guid Id,
    Guid ProjectId,
    Guid PredecessorId,
    Guid SuccessorId,
    DependencyType Type);

public sealed record PlannedTask(
    Guid TaskId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string Reason);

public sealed record PlanningIssue(
    string Code,
    string Message);

public sealed record PlanningResponse(
    string Driver,
    string Summary,
    IReadOnlyList<PlannedTask> Tasks,
    IReadOnlyList<PlanningIssue> Issues);
