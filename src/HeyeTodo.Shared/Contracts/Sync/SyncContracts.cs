namespace HeyeTodo.Shared.Contracts.Sync;

public enum ChangeEntityType
{
    Project = 0,
    TodoTask = 1,
    TaskDependency = 2,
}

public enum ChangeOperation
{
    Upsert = 0,
    Delete = 1,
}

public sealed record SyncChange(
    ChangeEntityType EntityType,
    ChangeOperation Operation,
    Guid EntityId,
    string PayloadJson,
    DateTimeOffset UpdatedAt,
    Guid UpdatedBy,
    Guid ClientId);

public sealed record SyncConflict(
    Guid EntityId,
    ChangeEntityType EntityType,
    string Reason,
    string? ServerPayloadJson);

public sealed record SyncPushRequest(
    Guid ClientId,
    IReadOnlyList<SyncChange> Changes);

public sealed record SyncPushResponse(
    long ServerVersion,
    IReadOnlyList<Guid> AcceptedChangeIds,
    IReadOnlyList<SyncConflict> Conflicts);

public sealed record SyncPullResponse(
    long ServerVersion,
    IReadOnlyList<SyncChange> Changes);
