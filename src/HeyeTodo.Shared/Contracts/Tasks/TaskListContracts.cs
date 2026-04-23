using System;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Contracts.Tasks;

public enum TaskSortField
{
    UpdatedAt = 0,
    Title = 1,
    Priority = 2,
    Status = 3,
    StartDate = 4,
    EndDate = 5,
}

public enum SortDirection
{
    Ascending = 0,
    Descending = 1,
}

public sealed record TaskListQuery(
    Guid? ProjectId,
    TaskStatus? Status,
    TaskPriority? Priority,
    string? Search,
    TaskSortField SortBy,
    SortDirection SortDirection,
    bool IncludeCompleted = true);
