namespace HeyeTodo.Shared.Sync;

public sealed class SyncMeta
{
    public long ServerVersion { get; init; }
    public DateTimeOffset UpdatedAt { get; init; }
    public Guid UpdatedBy { get; init; }
    public Guid ClientId { get; init; }
    public DateTimeOffset? DeletedAt { get; init; }
}
