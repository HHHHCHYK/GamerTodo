using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;

namespace HeyeTodo.Client.Data.Repositories;

public interface ITaskRepository
{
    Task<IReadOnlyList<LocalTask>> ListAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default);
    Task<LocalTask?> GetAsync(Guid ownerId, Guid taskId, CancellationToken ct = default);
    Task<LocalTask> CreateAsync(Guid ownerId, CreateTaskRequest request, Guid clientId, CancellationToken ct = default);
    Task<LocalTask?> UpdateAsync(Guid ownerId, UpdateTaskRequest request, Guid clientId, CancellationToken ct = default);
    Task<LocalTask?> ChangeStatusAsync(Guid ownerId, ChangeTaskStatusRequest request, Guid clientId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid ownerId, Guid taskId, Guid clientId, CancellationToken ct = default);
    Task<LocalTask?> UpdateSyncMetadataAsync(Guid ownerId, Guid taskId, SyncMeta sync, CancellationToken ct = default);
    Task UpsertFromRemoteAsync(Guid ownerId, IReadOnlyList<TaskDto> tasks, CancellationToken ct = default);
}
