using System.Text.Json;
using HeyeTodo.Server.Api.Hubs;
using HeyeTodo.Server.Domain.Entities;
using HeyeTodo.Server.Infrastructure.Persistence;
using HeyeTodo.Shared.Contracts.Sync;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Server.Application.Sync;

public sealed class SyncService : ISyncService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AppDbContext _db;
    private readonly IHubContext<SyncHub> _hub;

    public SyncService(AppDbContext db, IHubContext<SyncHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<SyncPushResponse> PushAsync(Guid userId, Guid clientId, SyncPushRequest request, CancellationToken ct = default)
    {
        var acceptedIds = new List<Guid>();
        var conflicts = new List<SyncConflict>();
        var invalidatedProjectIds = new HashSet<Guid>();

        foreach (var change in request.Changes)
        {
            if (change.EntityType == ChangeEntityType.Project)
            {
                await ApplyProjectChangeAsync(userId, clientId, change, acceptedIds, conflicts, invalidatedProjectIds, ct);
                continue;
            }

            if (change.EntityType == ChangeEntityType.TodoTask)
            {
                await ApplyTaskChangeAsync(userId, clientId, change, acceptedIds, conflicts, invalidatedProjectIds, ct);
                continue;
            }

            conflicts.Add(new SyncConflict(change.EntityId, change.EntityType, "Unsupported entity type.", null));
        }

        await _db.SaveChangesAsync(ct);

        foreach (var projectId in invalidatedProjectIds)
        {
            await _hub.Clients.Group(SyncHub.GroupName(projectId))
                .SendAsync(SyncHub.ProjectInvalidatedMethod, projectId, ct);
        }

        var serverVersion = await GetCurrentServerVersionAsync(userId, ct);
        return new SyncPushResponse(serverVersion, acceptedIds, conflicts);
    }

    public async Task<SyncPullResponse> PullAsync(Guid userId, long sinceServerVersion, CancellationToken ct = default)
    {
        var changes = new List<SyncChange>();

        var projects = await _db.Projects
            .Where(x => x.OwnerId == userId && x.ServerVersion > sinceServerVersion)
            .OrderBy(x => x.ServerVersion)
            .ToListAsync(ct);

        foreach (var project in projects)
        {
            changes.Add(new SyncChange(
                ChangeEntityType.Project,
                project.DeletedAt is null ? ChangeOperation.Upsert : ChangeOperation.Delete,
                project.Id,
                SerializeProject(project),
                project.UpdatedAt,
                project.UpdatedBy,
                project.ClientId));
        }

        var projectIds = await _db.Projects
            .Where(x => x.OwnerId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var tasks = await _db.Tasks
            .Where(x => projectIds.Contains(x.ProjectId) && x.ServerVersion > sinceServerVersion)
            .OrderBy(x => x.ServerVersion)
            .ToListAsync(ct);

        foreach (var task in tasks)
        {
            changes.Add(new SyncChange(
                ChangeEntityType.TodoTask,
                task.DeletedAt is null ? ChangeOperation.Upsert : ChangeOperation.Delete,
                task.Id,
                SerializeTask(task),
                task.UpdatedAt,
                task.UpdatedBy,
                task.ClientId));
        }

        var serverVersion = changes.Count == 0
            ? await GetCurrentServerVersionAsync(userId, ct)
            : await GetCurrentServerVersionAsync(userId, ct);

        return new SyncPullResponse(serverVersion, changes.OrderBy(x => x.UpdatedAt).ToList());
    }

    private async Task ApplyProjectChangeAsync(
        Guid userId,
        Guid clientId,
        SyncChange change,
        List<Guid> acceptedIds,
        List<SyncConflict> conflicts,
        HashSet<Guid> invalidatedProjectIds,
        CancellationToken ct)
    {
        var entity = await _db.Projects.FirstOrDefaultAsync(x => x.Id == change.EntityId && x.OwnerId == userId, ct);
        if (entity is not null && entity.UpdatedAt > change.UpdatedAt)
        {
            conflicts.Add(new SyncConflict(change.EntityId, change.EntityType, "Server version is newer.", SerializeProject(entity)));
            return;
        }

        if (change.Operation == ChangeOperation.Delete)
        {
            if (entity is null)
            {
                acceptedIds.Add(change.EntityId);
                return;
            }

            entity.DeletedAt = change.UpdatedAt;
            entity.UpdatedAt = change.UpdatedAt;
            entity.UpdatedBy = userId;
            entity.ClientId = clientId;
            StampVersion(entity);

            foreach (var task in await _db.Tasks.Where(x => x.ProjectId == entity.Id && x.DeletedAt == null).ToListAsync(ct))
            {
                task.DeletedAt = change.UpdatedAt;
                task.UpdatedAt = change.UpdatedAt;
                task.UpdatedBy = userId;
                task.ClientId = clientId;
                StampVersion(task);
            }

            acceptedIds.Add(change.EntityId);
            invalidatedProjectIds.Add(entity.Id);
            return;
        }

        var payload = Deserialize<ProjectDto>(change.PayloadJson);
        if (payload is null)
        {
            conflicts.Add(new SyncConflict(change.EntityId, change.EntityType, "Invalid project payload.", entity is null ? null : SerializeProject(entity)));
            return;
        }

        if (entity is null)
        {
            entity = new Project
            {
                Id = payload.Id,
                OwnerId = userId,
                CreatedAt = payload.CreatedAt,
            };
            _db.Projects.Add(entity);
        }

        entity.Name = payload.Name.Trim();
        entity.Description = NormalizeNullable(payload.Description);
        entity.UpdatedAt = change.UpdatedAt;
        entity.UpdatedBy = userId;
        entity.ClientId = clientId;
        entity.DeletedAt = payload.Sync.DeletedAt;
        StampVersion(entity);

        acceptedIds.Add(change.EntityId);
        invalidatedProjectIds.Add(entity.Id);
    }

    private async Task ApplyTaskChangeAsync(
        Guid userId,
        Guid clientId,
        SyncChange change,
        List<Guid> acceptedIds,
        List<SyncConflict> conflicts,
        HashSet<Guid> invalidatedProjectIds,
        CancellationToken ct)
    {
        var entity = await _db.Tasks
            .Join(
                _db.Projects.Where(p => p.OwnerId == userId),
                task => task.ProjectId,
                project => project.Id,
                (task, _) => task)
            .FirstOrDefaultAsync(x => x.Id == change.EntityId, ct);

        if (entity is not null && entity.UpdatedAt > change.UpdatedAt)
        {
            conflicts.Add(new SyncConflict(change.EntityId, change.EntityType, "Server version is newer.", SerializeTask(entity)));
            return;
        }

        if (change.Operation == ChangeOperation.Delete)
        {
            if (entity is null)
            {
                acceptedIds.Add(change.EntityId);
                return;
            }

            entity.DeletedAt = change.UpdatedAt;
            entity.UpdatedAt = change.UpdatedAt;
            entity.UpdatedBy = userId;
            entity.ClientId = clientId;
            StampVersion(entity);
            acceptedIds.Add(change.EntityId);
            invalidatedProjectIds.Add(entity.ProjectId);
            return;
        }

        var payload = Deserialize<TaskDto>(change.PayloadJson);
        if (payload is null)
        {
            conflicts.Add(new SyncConflict(change.EntityId, change.EntityType, "Invalid task payload.", entity is null ? null : SerializeTask(entity)));
            return;
        }

        var project = await _db.Projects.FirstOrDefaultAsync(x => x.Id == payload.ProjectId && x.OwnerId == userId && x.DeletedAt == null, ct);
        if (project is null)
        {
            conflicts.Add(new SyncConflict(change.EntityId, change.EntityType, "Project not found.", null));
            return;
        }

        if (entity is null)
        {
            entity = new TodoTask
            {
                Id = payload.Id,
            };
            _db.Tasks.Add(entity);
        }

        entity.ProjectId = payload.ProjectId;
        entity.Title = payload.Title.Trim();
        entity.Description = NormalizeNullable(payload.Description);
        entity.Status = payload.Status;
        entity.Priority = payload.Priority;
        entity.StartDate = payload.StartDate;
        entity.EndDate = payload.EndDate;
        entity.EstimatedHours = payload.EstimatedHours;
        entity.AssigneeId = payload.AssigneeId;
        entity.RoleFieldsJson = payload.RoleFields is null ? null : JsonSerializer.Serialize(payload.RoleFields, JsonOptions);
        entity.UpdatedAt = change.UpdatedAt;
        entity.UpdatedBy = userId;
        entity.ClientId = clientId;
        entity.DeletedAt = payload.Sync.DeletedAt;
        StampVersion(entity);

        acceptedIds.Add(change.EntityId);
        invalidatedProjectIds.Add(entity.ProjectId);
    }

    private async Task<long> GetCurrentServerVersionAsync(Guid userId, CancellationToken ct)
    {
        var projectVersion = await _db.Projects
            .Where(x => x.OwnerId == userId)
            .Select(x => (long?)x.ServerVersion)
            .MaxAsync(ct) ?? 0L;

        var projectIds = await _db.Projects
            .Where(x => x.OwnerId == userId)
            .Select(x => x.Id)
            .ToListAsync(ct);

        var taskVersion = projectIds.Count == 0
            ? 0L
            : await _db.Tasks
                .Where(x => projectIds.Contains(x.ProjectId))
                .Select(x => (long?)x.ServerVersion)
                .MaxAsync(ct) ?? 0L;

        return Math.Max(projectVersion, taskVersion);
    }

    private static T? Deserialize<T>(string json)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(json, JsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private static string SerializeProject(Project entity)
        => JsonSerializer.Serialize(new ProjectDto(
            entity.Id,
            entity.OwnerId,
            entity.Name,
            entity.Description,
            entity.CreatedAt,
            new SyncMeta
            {
                ServerVersion = entity.ServerVersion,
                UpdatedAt = entity.UpdatedAt,
                UpdatedBy = entity.UpdatedBy,
                ClientId = entity.ClientId,
                DeletedAt = entity.DeletedAt,
            }), JsonOptions);

    private static string SerializeTask(TodoTask entity)
        => JsonSerializer.Serialize(new TaskDto(
            entity.Id,
            entity.ProjectId,
            entity.Title,
            entity.Description,
            entity.Status,
            entity.Priority,
            entity.StartDate,
            entity.EndDate,
            entity.EstimatedHours,
            entity.AssigneeId,
            DeserializeRoleFields(entity.RoleFieldsJson),
            new SyncMeta
            {
                ServerVersion = entity.ServerVersion,
                UpdatedAt = entity.UpdatedAt,
                UpdatedBy = entity.UpdatedBy,
                ClientId = entity.ClientId,
                DeletedAt = entity.DeletedAt,
            }), JsonOptions);

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

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static void StampVersion(SyncableEntity entity)
        => entity.ServerVersion = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
}
