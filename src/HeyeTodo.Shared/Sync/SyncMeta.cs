using System;

namespace HeyeTodo.Shared.Sync;

/// <summary>
/// Metadata attached to every syncable entity.
/// Used for Last-Write-Wins conflict resolution and tombstones.
/// </summary>
public sealed class SyncMeta
{
    /// <summary>Monotonic version assigned by the server on accept.</summary>
    public long ServerVersion { get; set; }

    /// <summary>Last local modification time (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>User id that last modified the entity.</summary>
    public Guid UpdatedBy { get; set; }

    /// <summary>Installation / device id that produced the change.</summary>
    public Guid ClientId { get; set; }

    /// <summary>Soft-delete timestamp; null when active.</summary>
    public DateTimeOffset? DeletedAt { get; set; }
}
