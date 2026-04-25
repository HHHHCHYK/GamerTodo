namespace HeyeTodo.Client.Application.Sync;

public interface ISyncCoordinator
{
    event Action<Guid>? ProjectInvalidated;

    Task<SyncRunResult> SyncNowAsync(Guid ownerId, CancellationToken ct = default);
    Task<SyncRunResult> PullAsync(Guid ownerId, CancellationToken ct = default);
    Task StartAsync(Guid ownerId, CancellationToken ct = default);
    Task StopAsync(CancellationToken ct = default);
    Task SubscribeProjectAsync(Guid projectId, CancellationToken ct = default);
    Task UnsubscribeProjectAsync(Guid projectId, CancellationToken ct = default);
}

public sealed record SyncRunResult(bool PushSucceeded, bool PullSucceeded, string? Warning = null)
{
    public bool Succeeded => PushSucceeded && PullSucceeded;
}
