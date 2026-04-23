using System;
using System.Collections.Generic;
using System.Text.Json;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;

namespace HeyeTodo.Client.Application.Tasks;

public sealed class TaskWorkspaceService : ITaskWorkspaceService
{
    private readonly IProjectRepository _projects;
    private readonly ITaskRepository _tasks;
    private readonly ProjectApiClient _projectApi;
    private readonly TaskApiClient _taskApi;

    public TaskWorkspaceService(
        IProjectRepository projects,
        ITaskRepository tasks,
        ProjectApiClient projectApi,
        TaskApiClient taskApi)
    {
        _projects = projects;
        _tasks = tasks;
        _projectApi = projectApi;
        _taskApi = taskApi;
    }

    public Task<IReadOnlyList<LocalProject>> ListProjectsAsync(Guid ownerId, CancellationToken ct = default)
        => _projects.ListAsync(ownerId, ct);

    public Task<IReadOnlyList<LocalTask>> ListTasksAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
        => _tasks.ListAsync(ownerId, query, ct);

    public async Task<WorkspaceMutationResult<LocalProject>> CreateProjectAsync(Guid ownerId, CreateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _projects.CreateAsync(ownerId, request, clientId, ct);
        try
        {
            var remote = await _projectApi.CreateProjectAsync(request, ct);
            if (remote is null)
            {
                return new WorkspaceMutationResult<LocalProject>(local, false, "Remote sync failed.");
            }

            var updated = await _projects.UpdateSyncMetadataAsync(ownerId, local.Id, remote.Sync, ct) ?? local;
            return new WorkspaceMutationResult<LocalProject>(updated, true);
        }
        catch
        {
            return new WorkspaceMutationResult<LocalProject>(local, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceMutationResult<LocalProject>?> UpdateProjectAsync(Guid ownerId, Guid projectId, UpdateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _projects.UpdateAsync(ownerId, request with { Id = projectId }, clientId, ct);
        if (local is null)
        {
            return null;
        }

        try
        {
            var remote = await _projectApi.UpdateProjectAsync(projectId, request with { Id = projectId }, ct);
            if (remote is null)
            {
                return new WorkspaceMutationResult<LocalProject>(local, false, "Remote sync failed.");
            }

            var updated = await _projects.UpdateSyncMetadataAsync(ownerId, local.Id, remote.Sync, ct) ?? local;
            return new WorkspaceMutationResult<LocalProject>(updated, true);
        }
        catch
        {
            return new WorkspaceMutationResult<LocalProject>(local, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceActionResult> DeleteProjectAsync(Guid ownerId, Guid projectId, Guid clientId, CancellationToken ct = default)
    {
        var deleted = await _projects.DeleteAsync(ownerId, projectId, clientId, ct);
        if (!deleted)
        {
            return new WorkspaceActionResult(false, false);
        }

        try
        {
            var remoteDeleted = await _projectApi.DeleteProjectAsync(projectId, ct);
            return new WorkspaceActionResult(true, remoteDeleted, remoteDeleted ? null : "Remote sync failed.");
        }
        catch
        {
            return new WorkspaceActionResult(true, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceMutationResult<LocalTask>> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.CreateAsync(ownerId, request, clientId, ct);
        try
        {
            var remote = await _taskApi.CreateTaskAsync(request, ct);
            if (remote is null)
            {
                return new WorkspaceMutationResult<LocalTask>(local, false, "Remote sync failed.");
            }

            var updated = await _tasks.UpdateSyncMetadataAsync(ownerId, local.Id, remote.Sync, ct) ?? local;
            return new WorkspaceMutationResult<LocalTask>(updated, true);
        }
        catch
        {
            return new WorkspaceMutationResult<LocalTask>(local, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> UpdateTaskAsync(Guid ownerId, Guid taskId, UpdateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.UpdateAsync(ownerId, request with { Id = taskId }, clientId, ct);
        if (local is null)
        {
            return null;
        }

        try
        {
            var remote = await _taskApi.UpdateTaskAsync(taskId, request with { Id = taskId }, ct);
            if (remote is null)
            {
                return new WorkspaceMutationResult<LocalTask>(local, false, "Remote sync failed.");
            }

            var updated = await _tasks.UpdateSyncMetadataAsync(ownerId, local.Id, remote.Sync, ct) ?? local;
            return new WorkspaceMutationResult<LocalTask>(updated, true);
        }
        catch
        {
            return new WorkspaceMutationResult<LocalTask>(local, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceMutationResult<LocalTask>?> ChangeTaskStatusAsync(Guid ownerId, Guid taskId, ChangeTaskStatusRequest request, Guid clientId, CancellationToken ct = default)
    {
        var local = await _tasks.ChangeStatusAsync(ownerId, request with { Id = taskId }, clientId, ct);
        if (local is null)
        {
            return null;
        }

        try
        {
            var remote = await _taskApi.ChangeTaskStatusAsync(taskId, request with { Id = taskId }, ct);
            if (remote is null)
            {
                return new WorkspaceMutationResult<LocalTask>(local, false, "Remote sync failed.");
            }

            var updated = await _tasks.UpdateSyncMetadataAsync(ownerId, local.Id, remote.Sync, ct) ?? local;
            return new WorkspaceMutationResult<LocalTask>(updated, true);
        }
        catch
        {
            return new WorkspaceMutationResult<LocalTask>(local, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceActionResult> DeleteTaskAsync(Guid ownerId, Guid taskId, Guid clientId, CancellationToken ct = default)
    {
        var deleted = await _tasks.DeleteAsync(ownerId, taskId, clientId, ct);
        if (!deleted)
        {
            return new WorkspaceActionResult(false, false);
        }

        try
        {
            var remoteDeleted = await _taskApi.DeleteTaskAsync(taskId, ct);
            return new WorkspaceActionResult(true, remoteDeleted, remoteDeleted ? null : "Remote sync failed.");
        }
        catch
        {
            return new WorkspaceActionResult(true, false, "Remote sync failed.");
        }
    }

    public async Task<WorkspaceRefreshResult> RefreshAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
    {
        var syncedProjects = false;
        var syncedTasks = false;
        string? warning = null;

        try
        {
            var remoteProjects = await _projectApi.GetProjectsAsync(ct);
            if (remoteProjects is not null)
            {
                await _projects.UpsertFromRemoteAsync(ownerId, remoteProjects, ct);
                syncedProjects = true;
            }
            else
            {
                warning = "Project refresh failed.";
            }
        }
        catch
        {
            warning = "Project refresh failed.";
        }

        try
        {
            var remoteTasks = await _taskApi.GetTasksAsync(query, ct);
            if (remoteTasks is not null)
            {
                await _tasks.UpsertFromRemoteAsync(ownerId, remoteTasks, ct);
                syncedTasks = true;
            }
            else
            {
                warning = warning is null ? "Task refresh failed." : warning + " Task refresh failed.";
            }
        }
        catch
        {
            warning = warning is null ? "Task refresh failed." : warning + " Task refresh failed.";
        }

        return new WorkspaceRefreshResult(syncedProjects, syncedTasks, warning);
    }
}
