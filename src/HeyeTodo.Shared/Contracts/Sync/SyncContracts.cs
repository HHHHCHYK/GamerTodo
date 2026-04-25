using System;
using System.Collections.Generic;

namespace HeyeTodo.Shared.Contracts.Sync;

/// <summary>
/// Envelope sent from client to server describing local changes to push.
/// </summary>
public sealed record SyncPushRequest(
    Guid ClientId,
    long SinceServerVersion,
    IReadOnlyList<SyncChange> Changes);

public sealed record SyncPushResponse(
    long ServerVersion,
    IReadOnlyList<Guid> AcceptedIds,
    IReadOnlyList<SyncConflict> Conflicts);

public sealed record SyncPullResponse(
    long ServerVersion,
    IReadOnlyList<SyncChange> Changes);

public enum ChangeEntityType
{
    Project = 1,
    TodoTask = 2,
    TaskDependency = 3,
}

public enum ChangeOperation
{
    Upsert = 1,
    Delete = 2,
}

/// <summary>
/// Wire representation of a single entity mutation.
/// Payload is JSON-serialized DTO appropriate for <see cref="EntityType"/>.
/// </summary>
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
