namespace HeyeTodo.Client.Persistence;

public interface ITaskRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<TaskWorkspaceState> LoadWorkspaceAsync(CancellationToken cancellationToken = default);

    Task SaveProjectAsync(TaskProjectRecord project, bool enqueueSyncChange, CancellationToken cancellationToken = default);

    Task SaveTaskAsync(TaskItemRecord task, bool enqueueSyncChange, CancellationToken cancellationToken = default);

    Task ApplyRemoteProjectAsync(TaskProjectRecord project, CancellationToken cancellationToken = default);

    Task ApplyRemoteTaskAsync(TaskItemRecord task, CancellationToken cancellationToken = default);

    Task ApplyRemoteDeleteAsync(string entityId, HeyeTodo.Shared.Contracts.Sync.ChangeEntityType entityType, DateTimeOffset deletedAt, CancellationToken cancellationToken = default);

    Task SoftDeleteTaskAsync(string taskId, DateTimeOffset deletedAt, CancellationToken cancellationToken = default);

    Task ReplaceWorkspaceFromLocalImportAsync(TaskWorkspaceState state, CancellationToken cancellationToken = default);

    Task<long> GetLastPulledServerVersionAsync(CancellationToken cancellationToken = default);

    Task SetLastPulledServerVersionAsync(long serverVersion, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SyncOutboxRecord>> LoadPendingOutboxAsync(CancellationToken cancellationToken = default);

    Task DeleteOutboxEntriesAsync(IEnumerable<long> ids, CancellationToken cancellationToken = default);

    Task<string?> GetSettingAsync(string key, CancellationToken cancellationToken = default);

    Task SetSettingAsync(string key, string value, CancellationToken cancellationToken = default);
}
