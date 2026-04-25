using System;
using HeyeTodo.Shared.Contracts.Sync;
using HeyeTodo.Shared.Enums;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.Data.Entities;

public abstract class LocalSyncable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public long ServerVersion { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedBy { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public bool IsDirty { get; set; }
}

public sealed class LocalProject : LocalSyncable
{
    public Guid OwnerId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class LocalTask : LocalSyncable
{
    public Guid ProjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Backlog;
    public TaskPriority Priority { get; set; } = TaskPriority.Normal;
    public DateTimeOffset? StartDate { get; set; }
    public DateTimeOffset? EndDate { get; set; }
    public double? EstimatedHours { get; set; }
    public Guid? AssigneeId { get; set; }
    public string? RoleFieldsJson { get; set; }
}

public sealed class LocalDependency : LocalSyncable
{
    public Guid ProjectId { get; set; }
    public Guid PredecessorId { get; set; }
    public Guid SuccessorId { get; set; }
    public DependencyType Type { get; set; } = DependencyType.FinishToStart;
}

public sealed class LocalOutboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public ChangeEntityType EntityType { get; set; }
    public ChangeOperation Operation { get; set; }
    public Guid EntityId { get; set; }
    public string PayloadJson { get; set; } = null!;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset EnqueuedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; set; }
    public string? ConflictReason { get; set; }
}

public sealed class LocalInboxItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid OwnerId { get; set; }
    public ChangeEntityType EntityType { get; set; }
    public ChangeOperation Operation { get; set; }
    public Guid EntityId { get; set; }
    public long ServerVersion { get; set; }
    public string PayloadJson { get; set; } = null!;
    public DateTimeOffset UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AppliedAt { get; set; }
}
