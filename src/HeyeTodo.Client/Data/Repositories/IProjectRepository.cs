using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;

namespace HeyeTodo.Client.Data.Repositories;

public interface IProjectRepository
{
    Task<IReadOnlyList<LocalProject>> ListAsync(Guid ownerId, CancellationToken ct = default);
    Task<LocalProject> CreateAsync(Guid ownerId, CreateProjectRequest request, Guid clientId, CancellationToken ct = default);
    Task<LocalProject?> UpdateAsync(Guid ownerId, UpdateProjectRequest request, Guid clientId, CancellationToken ct = default);
    Task<bool> DeleteAsync(Guid ownerId, Guid projectId, Guid clientId, CancellationToken ct = default);
    Task<LocalProject?> UpdateSyncMetadataAsync(Guid ownerId, Guid projectId, SyncMeta sync, CancellationToken ct = default);
    Task UpsertFromRemoteAsync(Guid ownerId, IReadOnlyList<ProjectDto> projects, CancellationToken ct = default);
}
