using System;
using System.Collections.Generic;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Application.Tasks;

public sealed class TaskWorkspaceService : ITaskWorkspaceService
{
    private readonly IProjectRepository _projects;
    private readonly ITaskRepository _tasks;
    private readonly ISyncCoordinator _sync;

    public TaskWorkspaceService(
        IProjectRepository projects,
        ITaskRepository tasks,
        ISyncCoordinator sync)
    {
        _projects = projects;
        _tasks = tasks;
        _sync = sync;
    }

    public Task<IReadOnlyList<LocalProject>> ListProjectsAsync(Guid ownerId, CancellationToken ct = default)
        => _projects.ListAsync(ownerId, ct);

    public Task<IReadOnlyList<LocalTask>> ListTasksAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
        => _tasks.ListAsync(ownerId, query, ct);

    public async Task<WorkspaceMutationResult<LocalProject>> CreateProjectAsync(Guid ownerId, CreateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _projects.CreateAsync(ownerId, request, clientId, ct);
        var sync = await _sync.SyncNowAsync(ownerId, ct);
        var refreshed = await _projects.ListAsync(ownerId, ct);
        var current = refreshed.FirstOrDefault(x => x.Id == local.Id) ?? local;
        return new WorkspaceMutationResult<LocalProject>(current, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalProject>?> UpdateProjectAsync(Guid ownerId, Guid projectId, UpdateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _projects.UpdateAsync(ownerId, request with { Id = projectId }, clientId, ct);
        if (local is null)
        {
            return null;
        }

        var sync = await _sync.SyncNowAsync(ownerId, ct);
        var refreshed = await _projects.ListAsync(ownerId, ct);
        var current = refreshed.FirstOrDefault(x => x.Id == local.Id) ?? local;
        return new WorkspaceMutationResult<LocalProject>(current, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceActionResult> DeleteProjectAsync(Guid ownerId, Guid projectId, Guid clientId, CancellationToken ct = default)
    {
        var deleted = await _projects.DeleteAsync(ownerId, projectId, clientId, ct);
        if (!deleted)
        {
            return new WorkspaceActionResult(false, false);
        }

        var sync = await _sync.SyncNowAsync(ownerId, ct);
        return new WorkspaceActionResult(true, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.CreateAsync(ownerId, request, clientId, ct);
        var sync = await _sync.SyncNowAsync(ownerId, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> UpdateTaskAsync(Guid ownerId, Guid taskId, UpdateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.UpdateAsync(ownerId, request with { Id = taskId }, clientId, ct);
        if (local is null)
        {
            return null;
        }

        var sync = await _sync.SyncNowAsync(ownerId, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> ChangeTaskStatusAsync(Guid ownerId, Guid taskId, ChangeTaskStatusRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.ChangeStatusAsync(ownerId, request with { Id = taskId }, clientId, ct);
        if (local is null)
        {
            return null;
        }

        var sync = await _sync.SyncNowAsync(ownerId, ct);
        var refreshed = await _tasks.GetAsync(ownerId, local.Id, ct) ?? local;
        return new WorkspaceMutationResult<LocalTask>(refreshed, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceActionResult> DeleteTaskAsync(Guid ownerId, Guid taskId, Guid clientId, CancellationToken ct = default)
    {
        var deleted = await _tasks.DeleteAsync(ownerId, taskId, clientId, ct);
        if (!deleted)
        {
            return new WorkspaceActionResult(false, false);
        }

        var sync = await _sync.SyncNowAsync(ownerId, ct);
        return new WorkspaceActionResult(true, sync.Succeeded, sync.Warning);
    }

    public async Task<WorkspaceRefreshResult> RefreshAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
    {
        _ = query;
        var sync = await _sync.SyncNowAsync(ownerId, ct);
        return new WorkspaceRefreshResult(sync.Succeeded, sync.Succeeded, sync.Warning);
    }
}
