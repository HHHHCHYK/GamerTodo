using HeyeTodo.Shared.Contracts.Sync;

namespace HeyeTodo.Client.Persistence;

public sealed record SyncOutboxRecord(
    long Id,
    ChangeEntityType EntityType,
    ChangeOperation Operation,
    string EntityId,
    string PayloadJson,
    DateTimeOffset UpdatedAt);
