using System;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Server.Domain.Entities;

public sealed class TaskDependency : SyncableEntity
{
    public Guid ProjectId { get; set; }
    public Project? Project { get; set; }

    public Guid PredecessorId { get; set; }
    public TodoTask? Predecessor { get; set; }

    public Guid SuccessorId { get; set; }
    public TodoTask? Successor { get; set; }

    public DependencyType Type { get; set; } = DependencyType.FinishToStart;
}
