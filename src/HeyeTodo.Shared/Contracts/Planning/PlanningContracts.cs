using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Contracts.Planning;

public sealed record PlanningRequest(
    Guid? ProjectId,
    string Mode,
    DateTimeOffset AnchorDate,
    IReadOnlyList<PlanningTaskInput> Tasks,
    IReadOnlyList<PlanningDependencyInput> Dependencies,
    string? Prompt = null);

public sealed record PlanningTaskInput(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    double? EstimatedHours,
    IReadOnlyDictionary<string, object?>? RoleFields);

public sealed record PlanningDependencyInput(
    Guid Id,
    Guid ProjectId,
    Guid PredecessorId,
    Guid SuccessorId,
    DependencyType Type);

public sealed record PlanningResponse(
    string Driver,
    string Summary,
    IReadOnlyList<PlanningSuggestion> Suggestions,
    IReadOnlyList<PlanningIssue> Issues);

public sealed record PlanningSuggestion(
    Guid TaskId,
    int Rank,
    double Score,
    DateTimeOffset? SuggestedStartDate,
    DateTimeOffset? SuggestedEndDate,
    string Reason);

public sealed record PlanningIssue(
    string Code,
    string Message,
    Guid? TaskId = null);
