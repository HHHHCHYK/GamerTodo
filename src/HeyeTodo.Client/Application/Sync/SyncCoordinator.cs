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
    private readonly SyncOutboxStore _outboxStore;
    private readonly SyncInboxStore _inboxStore;
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
        SyncOutboxStore outboxStore,
        SyncInboxStore inboxStore,
        SignalRSyncClient signalR)
    {
        _dbFactory = dbFactory;
        _projects = projects;
        _tasks = tasks;
        _dependencies = dependencies;
        _syncApi = syncApi;
        _cursorStore = cursorStore;
        _outboxStore = outboxStore;
        _inboxStore = inboxStore;
        _signalR = signalR;
        _clientId = AppPaths.GetOrCreateClientId();
        _signalR.ProjectInvalidated += HandleProjectInvalidated;
    }

    public event Action<Guid>? ProjectInvalidated;

    public async Task<SyncRunResult> SyncNowAsync(Guid ownerId, CancellationToken ct = default)
    {
        try
        {
            var push = await PushAsync(ownerId, ct);
            var pull = await PullAsync(ownerId, ct);
            return new SyncRunResult(push, pull.PullSucceeded, push && pull.PullSucceeded ? null : pull.Warning ?? "Sync failed.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SyncRunResult(false, false, ex.Message);
        }
    }

    public async Task<SyncRunResult> PullAsync(Guid ownerId, CancellationToken ct = default)
    {
        try
        {
            var response = await _syncApi.PullAsync(_cursorStore.Load(), ct);
            if (response is null)
            {
                return new SyncRunResult(true, false, "Pull failed.");
            }

            await _inboxStore.RecordReceivedAsync(ownerId, response.Changes, ct);
            await ApplyRemoteChangesAsync(ownerId, response.Changes, ct);
            await _inboxStore.MarkAppliedAsync(ownerId, response.Changes, ct);
            _cursorStore.Save(response.ServerVersion);
            return new SyncRunResult(true, true);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new SyncRunResult(true, false, ex.Message);
        }
    }

    public async Task StartAsync(Guid ownerId, CancellationToken ct = default)
    {
        _ownerId = ownerId;
        try
        {
            await _signalR.EnsureConnectedAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }

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

        try
        {
            await _signalR.StopAsync(ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
    }

    public async Task SubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            await _signalR.SubscribeProjectAsync(projectId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
    }

    public async Task UnsubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        try
        {
            await _signalR.UnsubscribeProjectAsync(projectId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
        }
    }

    private async Task<bool> PushAsync(Guid ownerId, CancellationToken ct)
    {
        var changes = await CollectDirtyChangesAsync(ownerId, ct);
        await _outboxStore.RebuildPendingAsync(ownerId, changes, ct);
        changes = (await _outboxStore.LoadPendingChangesAsync(ownerId, ct)).ToList();
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
        await _outboxStore.MarkAcceptedAsync(ownerId, response.AcceptedIds, ct);
        await _outboxStore.MarkConflictsAsync(ownerId, response.Conflicts, ct);
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
        changes.AddRange(dirtyProjects.Select(SyncOutboxStore.MapProjectChange));
        changes.AddRange(dirtyTasks.Select(SyncOutboxStore.MapTaskChange));
        changes.AddRange(dirtyDependencies.Select(SyncOutboxStore.MapDependencyChange));
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
