using System.Text.Json;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Data;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Shared.Contracts.Sync;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SyncCoordinator : ISyncCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IDbContextFactory<LocalDbContext> _dbFactory;
    private readonly IProjectRepository _projects;
    private readonly ITaskRepository _tasks;
    private readonly IDependencyRepository _dependencies;
    private readonly SyncApiClient _syncApi;
    private readonly SyncCursorStore _cursorStore;
    private readonly SignalRSyncClient _signalR;
    private readonly Guid _clientId;
    private CancellationTokenSource? _loopCts;
    private Task? _loopTask;
    private Guid? _ownerId;

    public SyncCoordinator(
        IDbContextFactory<LocalDbContext> dbFactory,
        IProjectRepository projects,
        ITaskRepository tasks,
        IDependencyRepository dependencies,
        SyncApiClient syncApi,
        SyncCursorStore cursorStore,
        SignalRSyncClient signalR)
    {
        _dbFactory = dbFactory;
        _projects = projects;
        _tasks = tasks;
        _dependencies = dependencies;
        _syncApi = syncApi;
        _cursorStore = cursorStore;
        _signalR = signalR;
        _clientId = AppPaths.GetOrCreateClientId();
        _signalR.ProjectInvalidated += HandleProjectInvalidated;
    }

    public event Action<Guid>? ProjectInvalidated;

    public async Task<SyncRunResult> SyncNowAsync(Guid ownerId, CancellationToken ct = default)
    {
        var push = await PushAsync(ownerId, ct);
        var pull = await PullAsync(ownerId, ct);
        return new SyncRunResult(push, pull.PullSucceeded, push && pull.PullSucceeded ? null : pull.Warning ?? "Sync failed.");
    }

    public async Task<SyncRunResult> PullAsync(Guid ownerId, CancellationToken ct = default)
    {
        var response = await _syncApi.PullAsync(_cursorStore.Load(), ct);
        if (response is null)
        {
            return new SyncRunResult(true, false, "Pull failed.");
        }

        await ApplyRemoteChangesAsync(ownerId, response.Changes, ct);
        _cursorStore.Save(response.ServerVersion);
        return new SyncRunResult(true, true);
    }

    public async Task StartAsync(Guid ownerId, CancellationToken ct = default)
    {
        _ownerId = ownerId;
        await _signalR.EnsureConnectedAsync(ct);

        if (_loopTask is not null && !_loopTask.IsCompleted)
        {
            return;
        }

        _loopCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _loopTask = RunLoopAsync(ownerId, _loopCts.Token);
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_loopCts is not null)
        {
            _loopCts.Cancel();
        }

        if (_loopTask is not null)
        {
            try
            {
                await _loopTask;
            }
            catch (OperationCanceledException)
            {
            }
        }

        await _signalR.StopAsync(ct);
    }

    public Task SubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
        => _signalR.SubscribeProjectAsync(projectId, ct);

    public Task UnsubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
        => _signalR.UnsubscribeProjectAsync(projectId, ct);

    private async Task<bool> PushAsync(Guid ownerId, CancellationToken ct)
    {
        var changes = await CollectDirtyChangesAsync(ownerId, ct);
        if (changes.Count == 0)
        {
            return true;
        }

        var response = await _syncApi.PushAsync(new SyncPushRequest(_clientId, _cursorStore.Load(), changes), ct);
        if (response is null)
        {
            return false;
        }

        await ApplyPushResultAsync(ownerId, changes, response, ct);
        _cursorStore.Save(response.ServerVersion);
        return true;
    }

    private async Task<List<SyncChange>> CollectDirtyChangesAsync(Guid ownerId, CancellationToken ct)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var dirtyProjects = await db.Projects.Where(x => x.OwnerId == ownerId && x.IsDirty).ToListAsync(ct);

        var allOwnedProjectIds = await db.Projects.Where(x => x.OwnerId == ownerId).Select(x => x.Id).ToListAsync(ct);
        var dirtyTasks = await db.Tasks.Where(x => allOwnedProjectIds.Contains(x.ProjectId) && x.IsDirty).ToListAsync(ct);
        var dirtyDependencies = await db.Dependencies.Where(x => allOwnedProjectIds.Contains(x.ProjectId) && x.IsDirty).ToListAsync(ct);

        var changes = new List<SyncChange>(dirtyProjects.Count + dirtyTasks.Count + dirtyDependencies.Count);
        changes.AddRange(dirtyProjects.Select(MapProjectChange));
        changes.AddRange(dirtyTasks.Select(MapTaskChange));
        changes.AddRange(dirtyDependencies.Select(MapDependencyChange));
        return changes.OrderBy(x => x.UpdatedAt).ToList();
    }

    private async Task ApplyPushResultAsync(Guid ownerId, IReadOnlyList<SyncChange> submittedChanges, SyncPushResponse response, CancellationToken ct)
    {
        foreach (var acceptedId in response.AcceptedIds)
        {
            var change = submittedChanges.FirstOrDefault(x => x.EntityId == acceptedId);
            if (change is null)
            {
                continue;
            }

            var sync = new SyncMeta
            {
                ServerVersion = response.ServerVersion,
                UpdatedAt = change.UpdatedAt,
                UpdatedBy = change.UpdatedBy,
                ClientId = change.ClientId,
            };

            if (change.EntityType == ChangeEntityType.Project)
            {
                sync.DeletedAt = change.Operation == ChangeOperation.Delete ? change.UpdatedAt : null;
                await _projects.UpdateSyncMetadataAsync(ownerId, acceptedId, sync, ct);
            }
            else if (change.EntityType == ChangeEntityType.TodoTask)
            {
                sync.DeletedAt = change.Operation == ChangeOperation.Delete ? change.UpdatedAt : null;
                await _tasks.UpdateSyncMetadataAsync(ownerId, acceptedId, sync, ct);
            }
            else if (change.EntityType == ChangeEntityType.TaskDependency)
            {
                sync.DeletedAt = change.Operation == ChangeOperation.Delete ? change.UpdatedAt : null;
                await _dependencies.UpdateSyncMetadataAsync(ownerId, acceptedId, sync, ct);
            }
        }

        foreach (var conflict in response.Conflicts)
        {
            await ApplyConflictAsync(ownerId, conflict, ct);
        }
    }

    private async Task ApplyRemoteChangesAsync(Guid ownerId, IReadOnlyList<SyncChange> changes, CancellationToken ct)
    {
        var projects = new List<ProjectDto>();
        var tasks = new List<TaskDto>();
        var dependencies = new List<TaskDependencyDto>();

        foreach (var change in changes)
        {
            if (change.EntityType == ChangeEntityType.Project)
            {
                var dto = Deserialize<ProjectDto>(change.PayloadJson);
                if (dto is not null)
                {
                    projects.Add(EnsureProjectDeletedAt(dto, change));
                }
            }
            else if (change.EntityType == ChangeEntityType.TodoTask)
            {
                var dto = Deserialize<TaskDto>(change.PayloadJson);
                if (dto is not null)
                {
                    tasks.Add(EnsureTaskDeletedAt(dto, change));
                }
            }
            else if (change.EntityType == ChangeEntityType.TaskDependency)
            {
                var dto = Deserialize<TaskDependencyDto>(change.PayloadJson);
                if (dto is not null)
                {
                    dependencies.Add(EnsureDependencyDeletedAt(dto, change));
                }
            }
        }

        if (projects.Count > 0)
        {
            await _projects.UpsertFromRemoteAsync(ownerId, projects, ct);
        }

        if (tasks.Count > 0)
        {
            await _tasks.UpsertFromRemoteAsync(ownerId, tasks, ct);
        }

        if (dependencies.Count > 0)
        {
            await _dependencies.UpsertFromRemoteAsync(ownerId, dependencies, ct);
        }
    }

    private async Task ApplyConflictAsync(Guid ownerId, SyncConflict conflict, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(conflict.ServerPayloadJson))
        {
            return;
        }

        if (conflict.EntityType == ChangeEntityType.Project)
        {
            var project = Deserialize<ProjectDto>(conflict.ServerPayloadJson);
            if (project is not null)
            {
                await _projects.UpsertFromRemoteAsync(ownerId, new[] { project }, ct);
            }
            return;
        }

        if (conflict.EntityType == ChangeEntityType.TodoTask)
        {
            var task = Deserialize<TaskDto>(conflict.ServerPayloadJson);
            if (task is not null)
            {
                await _tasks.UpsertFromRemoteAsync(ownerId, new[] { task }, ct);
            }
            return;
        }

        if (conflict.EntityType == ChangeEntityType.TaskDependency)
        {
            var dependency = Deserialize<TaskDependencyDto>(conflict.ServerPayloadJson);
            if (dependency is not null)
            {
                await _dependencies.UpsertFromRemoteAsync(ownerId, new[] { dependency }, ct);
            }
        }
    }

    private async Task RunLoopAsync(Guid ownerId, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        while (await timer.WaitForNextTickAsync(ct))
        {
            try
            {
                await SyncNowAsync(ownerId, ct);
            }
            catch
            {
            }
        }
    }

    private async void HandleProjectInvalidated(Guid projectId)
    {
        ProjectInvalidated?.Invoke(projectId);
        if (_ownerId is null)
        {
            return;
        }

        try
        {
            await PullAsync(_ownerId.Value);
        }
        catch
        {
        }
    }

    private static SyncChange MapProjectChange(LocalProject project)
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

    private static SyncChange MapTaskChange(LocalTask task)
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

    private static SyncChange MapDependencyChange(LocalDependency dependency)
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

    private static ProjectDto EnsureProjectDeletedAt(ProjectDto dto, SyncChange change)
        => dto with
        {
            Sync = new SyncMeta
            {
                ServerVersion = dto.Sync.ServerVersion,
                UpdatedAt = dto.Sync.UpdatedAt,
                UpdatedBy = dto.Sync.UpdatedBy,
                ClientId = dto.Sync.ClientId,
                DeletedAt = change.Operation == ChangeOperation.Delete ? change.UpdatedAt : dto.Sync.DeletedAt,
            }
        };

    private static TaskDto EnsureTaskDeletedAt(TaskDto dto, SyncChange change)
        => dto with
        {
            Sync = new SyncMeta
            {
                ServerVersion = dto.Sync.ServerVersion,
                UpdatedAt = dto.Sync.UpdatedAt,
                UpdatedBy = dto.Sync.UpdatedBy,
                ClientId = dto.Sync.ClientId,
                DeletedAt = change.Operation == ChangeOperation.Delete ? change.UpdatedAt : dto.Sync.DeletedAt,
            }
        };

    private static TaskDependencyDto EnsureDependencyDeletedAt(TaskDependencyDto dto, SyncChange change)
        => dto with
        {
            Sync = new SyncMeta
            {
                ServerVersion = dto.Sync.ServerVersion,
                UpdatedAt = dto.Sync.UpdatedAt,
                UpdatedBy = dto.Sync.UpdatedBy,
                ClientId = dto.Sync.ClientId,
                DeletedAt = change.Operation == ChangeOperation.Delete ? change.UpdatedAt : dto.Sync.DeletedAt,
            }
        };

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
}
