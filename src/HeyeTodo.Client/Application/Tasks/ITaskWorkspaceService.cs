using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Application.Tasks;

public interface ITaskWorkspaceService
{
    Task<IReadOnlyList<LocalProject>> ListProjectsAsync(Guid ownerId, CancellationToken ct = default);
    Task<IReadOnlyList<LocalTask>> ListTasksAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default);
    Task<IReadOnlyList<LocalDependency>> ListDependenciesAsync(Guid ownerId, Guid? projectId = null, CancellationToken ct = default);
    Task<GanttWorkspaceSnapshot> GetGanttSnapshotAsync(Guid ownerId, Guid projectId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalTask>?> RescheduleTaskAsync(Guid ownerId, Guid taskId, DateTimeOffset? startDate, DateTimeOffset? endDate, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalTask>?> UpdateTaskRoleFieldsAsync(Guid ownerId, Guid taskId, string? roleFieldsJson, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalProject>> CreateProjectAsync(Guid ownerId, CreateProjectRequest request, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalProject>?> UpdateProjectAsync(Guid ownerId, Guid projectId, UpdateProjectRequest request, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceActionResult> DeleteProjectAsync(Guid ownerId, Guid projectId, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalTask>> CreateTaskAsync(Guid ownerId, CreateTaskRequest request, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalTask>?> UpdateTaskAsync(Guid ownerId, Guid taskId, UpdateTaskRequest request, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceMutationResult<LocalTask>?> ChangeTaskStatusAsync(Guid ownerId, Guid taskId, ChangeTaskStatusRequest request, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceActionResult> DeleteTaskAsync(Guid ownerId, Guid taskId, Guid clientId, CancellationToken ct = default);
    Task<WorkspaceRefreshResult> RefreshAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default);
}

public sealed record WorkspaceMutationResult<T>(T Value, bool Synced, string? Warning = null);
public sealed record WorkspaceActionResult(bool Success, bool Synced, string? Warning = null);
public sealed record WorkspaceRefreshResult(bool SyncedProjects, bool SyncedTasks, string? Warning = null);
public sealed record GanttWorkspaceSnapshot(IReadOnlyList<LocalTask> Tasks, IReadOnlyList<LocalDependency> Dependencies);
