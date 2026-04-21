using System;

namespace HeyeTodo.Server.Domain.Entities;

public abstract class SyncableEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Server-assigned monotonic version used for pull cursor.</summary>
    public long ServerVersion { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
    public Guid UpdatedBy { get; set; }
    public Guid ClientId { get; set; }

    /// <summary>Soft-delete marker.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
