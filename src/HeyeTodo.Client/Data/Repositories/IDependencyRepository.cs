using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;

namespace HeyeTodo.Client.Data.Repositories;

public interface IDependencyRepository
{
    Task<IReadOnlyList<LocalDependency>> ListAsync(Guid ownerId, Guid? projectId = null, CancellationToken ct = default);
    Task UpsertFromRemoteAsync(Guid ownerId, IReadOnlyList<TaskDependencyDto> dependencies, CancellationToken ct = default);
    Task<LocalDependency?> UpdateSyncMetadataAsync(Guid ownerId, Guid dependencyId, SyncMeta sync, CancellationToken ct = default);
}
