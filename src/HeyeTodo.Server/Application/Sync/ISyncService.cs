using HeyeTodo.Shared.Contracts.Sync;

namespace HeyeTodo.Server.Application.Sync;

public interface ISyncService
{
    Task<SyncPushResponse> PushAsync(Guid userId, Guid clientId, SyncPushRequest request, CancellationToken ct = default);
    Task<SyncPullResponse> PullAsync(Guid userId, long sinceServerVersion, CancellationToken ct = default);
}
