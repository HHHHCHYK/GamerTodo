using System;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Shared.Contracts.Tasks;

public sealed record CreateTaskRequest(
    Guid ProjectId,
    string Title,
    string? Description,
    TaskStatus Status,
    TaskPriority Priority,
    DateTimeOffset? StartDate,
    DateTimeOffset? EndDate,
    double? EstimatedHours,
    Guid? AssigneeId,
    string? RoleFieldsJson);

public sealed record UpdateTaskRequest(
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
    string? RoleFieldsJson);

public sealed record ChangeTaskStatusRequest(
    Guid Id,
    TaskStatus Status);
