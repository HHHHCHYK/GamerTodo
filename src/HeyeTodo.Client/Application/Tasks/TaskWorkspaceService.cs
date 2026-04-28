using System;
using System.Collections.Generic;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Application.Tasks;

public sealed class TaskWorkspaceService : ITaskWorkspaceService
{
    private readonly IProjectRepository _projects;
    private readonly ITaskRepository _tasks;
    private readonly IDependencyRepository _dependencies;
    private readonly ISyncCoordinator _sync;
    private readonly IClientLogger _logger;

    public TaskWorkspaceService(
        IProjectRepository projects,
        ITaskRepository tasks,
        IDependencyRepository dependencies,
        ISyncCoordinator sync,
        IClientLogger logger)
    {
        _projects = projects;
        _tasks = tasks;
        _dependencies = dependencies;
        _sync = sync;
        _logger = logger;
    }

    public Task<IReadOnlyList<LocalProject>> ListProjectsAsync(Guid ownerId, CancellationToken ct = default)
        => _projects.ListAsync(ownerId, ct);

    public Task<IReadOnlyList<LocalTask>> ListTasksAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
        => _tasks.ListAsync(ownerId, query, ct);

    public Task<IReadOnlyList<LocalDependency>> ListDependenciesAsync(Guid ownerId, Guid? projectId = null, CancellationToken ct = default)
        => _dependencies.ListAsync(ownerId, projectId, ct);

    public async Task<GanttWorkspaceSnapshot> GetGanttSnapshotAsync(Guid ownerId, Guid projectId, CancellationToken ct = default)
    {
        var query = new TaskListQuery(projectId, null, null, null, TaskSortField.StartDate, SortDirection.Ascending, true);
        var tasks = await _tasks.ListAsync(ownerId, query, ct);
        var dependencies = await _dependencies.ListAsync(ownerId, projectId, ct);
        await _logger.LogOperationAsync("TaskWorkspace", "GetGanttSnapshot", ClientLogLevel.Information, "Gantt snapshot loaded.", new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["taskCount"] = tasks.Count,
            ["dependencyCount"] = dependencies.Count,
        }, ct: ct);
        return new GanttWorkspaceSnapshot(tasks, dependencies);
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> RescheduleTaskAsync(Guid ownerId, Guid taskId, DateTimeOffset? startDate, DateTimeOffset? endDate, Guid clientId, CancellationToken ct = default)
    {
        var current = await _tasks.GetAsync(ownerId, taskId, ct);
        if (current is null)
        {
            await LogMissingAsync("RescheduleTask", taskId, ct);
            return null;
        }

        var local = await _tasks.UpdateAsync(ownerId, new UpdateTaskRequest(
            current.Id,
            current.ProjectId,
            current.Title,
            current.Description,
            current.Status,
            current.Priority,
            startDate,
            endDate,
            current.EstimatedHours,
            current.AssigneeId,
            current.RoleFieldsJson), clientId, ct);
        if (local is null)
        {
            await LogMissingAsync("RescheduleTaskUpdate", taskId, ct);
            return null;
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("RescheduleTask", current.ProjectId, taskId, sync, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> UpdateTaskRoleFieldsAsync(Guid ownerId, Guid taskId, string? roleFieldsJson, Guid clientId, CancellationToken ct = default)
    {
        var current = await _tasks.GetAsync(ownerId, taskId, ct);
        if (current is null)
        {
            await LogMissingAsync("UpdateTaskRoleFields", taskId, ct);
            return null;
        }

        var local = await _tasks.UpdateAsync(ownerId, new UpdateTaskRequest(
            current.Id,
            current.ProjectId,
            current.Title,
            current.Description,
            current.Status,
            current.Priority,
            current.StartDate,
            current.EndDate,
            current.EstimatedHours,
            current.AssigneeId,
            roleFieldsJson), clientId, ct);
        if (local is null)
        {
            await LogMissingAsync("UpdateTaskRoleFieldsUpdate", taskId, ct);
            return null;
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("UpdateTaskRoleFields", current.ProjectId, taskId, sync, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalProject>> CreateProjectAsync(Guid ownerId, CreateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _projects.CreateAsync(ownerId, request, clientId, ct);
        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("CreateProject", local.Id, null, sync, ct);
        var refreshed = await _projects.ListAsync(ownerId, ct);
        var current = refreshed.FirstOrDefault(x => x.Id == local.Id) ?? local;
        return new WorkspaceMutationResult<LocalProject>(current, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalProject>?> UpdateProjectAsync(Guid ownerId, Guid projectId, UpdateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _projects.UpdateAsync(ownerId, request with { Id = projectId }, clientId, ct);
        if (local is null)
        {
            await LogMissingAsync("UpdateProject", projectId, ct);
            return null;
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("UpdateProject", projectId, null, sync, ct);
        var refreshed = await _projects.ListAsync(ownerId, ct);
        var current = refreshed.FirstOrDefault(x => x.Id == local.Id) ?? local;
        return new WorkspaceMutationResult<LocalProject>(current, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceActionResult> DeleteProjectAsync(Guid ownerId, Guid projectId, Guid clientId, CancellationToken ct = default)
    {
        var deleted = await _projects.DeleteAsync(ownerId, projectId, clientId, ct);
        if (!deleted)
        {
            await LogMissingAsync("DeleteProject", projectId, ct);
            return new WorkspaceActionResult(false, false);
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("DeleteProject", projectId, null, sync, ct);
        return new WorkspaceActionResult(true, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.CreateAsync(ownerId, request, clientId, ct);
        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("CreateTask", local.ProjectId, local.Id, sync, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> UpdateTaskAsync(Guid ownerId, Guid taskId, UpdateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.UpdateAsync(ownerId, request with { Id = taskId }, clientId, ct);
        if (local is null)
        {
            await LogMissingAsync("UpdateTask", taskId, ct);
            return null;
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("UpdateTask", local.ProjectId, taskId, sync, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> ChangeTaskStatusAsync(Guid ownerId, Guid taskId, ChangeTaskStatusRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.ChangeStatusAsync(ownerId, request with { Id = taskId }, clientId, ct);
        if (local is null)
        {
            await LogMissingAsync("ChangeTaskStatus", taskId, ct);
            return null;
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("ChangeTaskStatus", local.ProjectId, taskId, sync, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceActionResult> DeleteTaskAsync(Guid ownerId, Guid taskId, Guid clientId, CancellationToken ct = default)
    {
        var deleted = await _tasks.DeleteAsync(ownerId, taskId, clientId, ct);
        if (!deleted)
        {
            await LogMissingAsync("DeleteTask", taskId, ct);
            return new WorkspaceActionResult(false, false);
        }

        var sync = await TrySyncNowAsync(ownerId, ct);
        await LogMutationAsync("DeleteTask", null, taskId, sync, ct);
        return new WorkspaceActionResult(true, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceRefreshResult> RefreshAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
    {
        _ = query;
        var sync = await TrySyncNowAsync(ownerId, ct);
        await _logger.LogOperationAsync("TaskWorkspace", "Refresh", sync.Succeeded ? ClientLogLevel.Information : ClientLogLevel.Warning, "Workspace refresh completed.", new Dictionary<string, object?>
        {
            ["synced"] = sync.Succeeded,
            ["warning"] = sync.Warning,
        }, ct: ct);
        return new WorkspaceRefreshResult(sync.Succeeded, sync.Succeeded, sync.Warning);
    }

    private async Task<SyncRunResult> TrySyncNowAsync(Guid ownerId, CancellationToken ct)
    {
        try
        {
            return await _sync.SyncNowAsync(ownerId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await _logger.LogSyncOperationAsync("TaskWorkspaceSyncNow", ClientLogLevel.Error, "Immediate sync failed.", new Dictionary<string, object?>
            {
                ["ownerId"] = ownerId,
            }, ex, ct);
            return new SyncRunResult(false, false, ex.Message);
        }
    }

    private Task LogMutationAsync(string operation, Guid? projectId, Guid? taskId, SyncRunResult sync, CancellationToken ct)
        => _logger.LogOperationAsync("TaskWorkspace", operation, sync.Succeeded ? ClientLogLevel.Information : ClientLogLevel.Warning, "Workspace mutation completed.", new Dictionary<string, object?>
        {
            ["projectId"] = projectId,
            ["taskId"] = taskId,
            ["synced"] = sync.Succeeded,
            ["warning"] = sync.Warning,
        }, ct: ct);

    private Task LogMissingAsync(string operation, Guid entityId, CancellationToken ct)
        => _logger.LogOperationAsync("TaskWorkspace", operation, ClientLogLevel.Warning, "Workspace entity was not found.", new Dictionary<string, object?>
        {
            ["entityId"] = entityId,
        }, ct: ct);
}
