using System;
using System.Collections.Generic;
using HeyeTodo.Shared.Enums;
using HeyeTodo.Shared.Sync;

namespace HeyeTodo.Shared.Contracts.Tasks;

public sealed record ProjectDto(
    Guid Id,
    Guid OwnerId,
    string Name,
    string? Description,
    DateTimeOffset CreatedAt,
    SyncMeta Sync);

public sealed record TaskDto(
    Guid Id,
    Guid ProjectId,
    string Title,
    string? Description,
    HeyeTodo.Shared.Enums.TaskStatus Status,
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
