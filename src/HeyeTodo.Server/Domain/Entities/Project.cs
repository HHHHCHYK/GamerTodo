using System;
using System.Collections.Generic;

namespace HeyeTodo.Server.Domain.Entities;

public sealed class Project : SyncableEntity
{
    public Guid OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public string Name { get; set; } = null!;
    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public List<TodoTask> Tasks { get; set; } = new();
    public List<TaskDependency> Dependencies { get; set; } = new();
}
