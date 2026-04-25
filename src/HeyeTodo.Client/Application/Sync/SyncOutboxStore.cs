using System.Text.Json;
using HeyeTodo.Client.Data;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Sync;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SyncOutboxStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;

    public SyncOutboxStore(IDbContextFactory<LocalDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<SyncChange>> LoadPendingChangesAsync(Guid ownerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Outbox
            .Where(x => x.OwnerId == ownerId && x.AcknowledgedAt == null)
            .OrderBy(x => x.UpdatedAt)
            .ThenBy(x => x.EnqueuedAt)
            .Select(x => new SyncChange(x.EntityType, x.Operation, x.EntityId, x.PayloadJson, x.UpdatedAt, x.UpdatedBy, x.ClientId))
            .ToListAsync(ct);
    }

    public async Task RebuildPendingAsync(Guid ownerId, IReadOnlyList<SyncChange> changes, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var existing = await db.Outbox
            .Where(x => x.OwnerId == ownerId && x.AcknowledgedAt == null)
            .ToListAsync(ct);
        db.Outbox.RemoveRange(existing);

        foreach (var change in changes)
        {
            db.Outbox.Add(new LocalOutboxItem
            {
                OwnerId = ownerId,
                EntityType = change.EntityType,
                Operation = change.Operation,
                EntityId = change.EntityId,
                PayloadJson = change.PayloadJson,
                UpdatedAt = change.UpdatedAt,
                UpdatedBy = change.UpdatedBy,
                ClientId = change.ClientId,
            });
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkAcceptedAsync(Guid ownerId, IReadOnlyCollection<Guid> acceptedIds, CancellationToken ct = default)
    {
        if (acceptedIds.Count == 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var items = await db.Outbox
            .Where(x => x.OwnerId == ownerId && x.AcknowledgedAt == null && acceptedIds.Contains(x.EntityId))
            .ToListAsync(ct);

        foreach (var item in items)
        {
            item.AcknowledgedAt = now;
            item.ConflictReason = null;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task MarkConflictsAsync(Guid ownerId, IReadOnlyList<SyncConflict> conflicts, CancellationToken ct = default)
    {
        if (conflicts.Count == 0)
        {
            return;
        }

        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var conflict in conflicts)
        {
            var items = await db.Outbox
                .Where(x => x.OwnerId == ownerId && x.AcknowledgedAt == null && x.EntityType == conflict.EntityType && x.EntityId == conflict.EntityId)
                .ToListAsync(ct);

            foreach (var item in items)
            {
                item.AcknowledgedAt = now;
                item.ConflictReason = conflict.Reason;
            }
        }

        await db.SaveChangesAsync(ct);
    }

    public static SyncChange MapProjectChange(LocalProject project)
        => new(
            ChangeEntityType.Project,
            project.DeletedAt is null ? ChangeOperation.Upsert : ChangeOperation.Delete,
            project.Id,
            JsonSerializer.Serialize(new ProjectDto(
                project.Id,
                project.OwnerId,
                project.Name,
                project.Description,
                project.CreatedAt,
                new SyncMeta
                {
                    ServerVersion = project.ServerVersion,
                    UpdatedAt = project.UpdatedAt,
                    UpdatedBy = project.UpdatedBy,
                    ClientId = project.ClientId,
                    DeletedAt = project.DeletedAt,
                }), JsonOptions),
            project.UpdatedAt,
            project.UpdatedBy,
            project.ClientId);

    public static SyncChange MapTaskChange(LocalTask task)
        => new(
            ChangeEntityType.TodoTask,
            task.DeletedAt is null ? ChangeOperation.Upsert : ChangeOperation.Delete,
            task.Id,
            JsonSerializer.Serialize(new TaskDto(
                task.Id,
                task.ProjectId,
                task.Title,
                task.Description,
                task.Status,
                task.Priority,
                task.StartDate,
                task.EndDate,
                task.EstimatedHours,
                task.AssigneeId,
                DeserializeRoleFields(task.RoleFieldsJson),
                new SyncMeta
                {
                    ServerVersion = task.ServerVersion,
                    UpdatedAt = task.UpdatedAt,
                    UpdatedBy = task.UpdatedBy,
                    ClientId = task.ClientId,
                    DeletedAt = task.DeletedAt,
                }), JsonOptions),
            task.UpdatedAt,
            task.UpdatedBy,
            task.ClientId);

    public static SyncChange MapDependencyChange(LocalDependency dependency)
        => new(
            ChangeEntityType.TaskDependency,
            dependency.DeletedAt is null ? ChangeOperation.Upsert : ChangeOperation.Delete,
            dependency.Id,
            JsonSerializer.Serialize(new TaskDependencyDto(
                dependency.Id,
                dependency.ProjectId,
                dependency.PredecessorId,
                dependency.SuccessorId,
                dependency.Type,
                new SyncMeta
                {
                    ServerVersion = dependency.ServerVersion,
                    UpdatedAt = dependency.UpdatedAt,
                    UpdatedBy = dependency.UpdatedBy,
                    ClientId = dependency.ClientId,
                    DeletedAt = dependency.DeletedAt,
                }), JsonOptions),
            dependency.UpdatedAt,
            dependency.UpdatedBy,
            dependency.ClientId);

    private static IReadOnlyDictionary<string, object?>? DeserializeRoleFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }
}
