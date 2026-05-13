using HeyeTodo.Client.Persistence;
using HeyeTodo.Shared.Contracts.Sync;
using HeyeTodo.Shared.Contracts.Tasks;
using System.Text.Json;

namespace HeyeTodo.Client.Services;

public sealed class SyncCoordinator
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly ITaskRepository _repository;
    private readonly HeyeTodoApiClient _api;
    private readonly IClientSessionStore _sessionStore;
    private readonly SemaphoreSlim _gate = new(1, 1);

    public SyncCoordinator(ITaskRepository repository, HeyeTodoApiClient api, IClientSessionStore sessionStore)
    {
        _repository = repository;
        _api = api;
        _sessionStore = sessionStore;
    }

    public async Task<string> SyncOnceAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var session = await _sessionStore.LoadAsync(cancellationToken);
            if (!session.IsAuthenticated)
            {
                return "未登录，任务已保存在本地";
            }

            var pulledBeforePush = await _api.PullAsync(await _repository.GetLastPulledServerVersionAsync(cancellationToken), cancellationToken);
            await ApplyPulledChangesAsync(pulledBeforePush, cancellationToken);

            var outbox = await _repository.LoadPendingOutboxAsync(cancellationToken);
            if (outbox.Count > 0)
            {
                var changes = outbox.Select(change => new SyncChange(
                    change.EntityType,
                    change.Operation,
                    Guid.Parse(change.EntityId),
                    change.PayloadJson,
                    change.UpdatedAt,
                    Guid.Empty,
                    session.ClientId)).ToList();

                var pushed = await _api.PushAsync(new SyncPushRequest(session.ClientId, changes), cancellationToken);
                var acceptedEntityIds = pushed.AcceptedChangeIds.ToHashSet();
                await _repository.DeleteOutboxEntriesAsync(
                    outbox.Where(item => acceptedEntityIds.Contains(Guid.Parse(item.EntityId))).Select(item => item.Id),
                    cancellationToken);
            }

            var pulledAfterPush = await _api.PullAsync(await _repository.GetLastPulledServerVersionAsync(cancellationToken), cancellationToken);
            await ApplyPulledChangesAsync(pulledAfterPush, cancellationToken);

            return "已同步";
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task ApplyPulledChangesAsync(SyncPullResponse response, CancellationToken cancellationToken)
    {
        foreach (var change in response.Changes)
        {
            if (change.Operation == ChangeOperation.Delete)
            {
                await _repository.ApplyRemoteDeleteAsync(change.EntityId.ToString("D"), change.EntityType, change.UpdatedAt, cancellationToken);
                continue;
            }

            if (change.EntityType == ChangeEntityType.Project)
            {
                var project = JsonSerializer.Deserialize<ProjectDto>(change.PayloadJson, JsonOptions);
                if (project is null)
                {
                    continue;
                }

                await _repository.ApplyRemoteProjectAsync(new TaskProjectRecord(
                    project.Id.ToString("D"),
                    project.Name,
                    project.Description ?? string.Empty,
                    project.CreatedAt,
                    project.Sync.UpdatedAt,
                    project.Sync.ServerVersion,
                    project.Sync.DeletedAt), cancellationToken);
                continue;
            }

            if (change.EntityType == ChangeEntityType.TodoTask)
            {
                var task = JsonSerializer.Deserialize<TaskDto>(change.PayloadJson, JsonOptions);
                if (task is null)
                {
                    continue;
                }

                await _repository.ApplyRemoteTaskAsync(new TaskItemRecord(
                    task.Id.ToString("D"),
                    task.ProjectId.ToString("D"),
                    string.Empty,
                    task.Title,
                    task.Description ?? string.Empty,
                    task.Status == HeyeTodo.Shared.Enums.TaskStatus.Done,
                    0,
                    task.Sync.UpdatedAt,
                    task.Sync.UpdatedAt,
                    task.StartDate,
                    task.EndDate,
                    TryReadAssigneeName(task.RoleFields),
                    ToUrgency(task.Priority),
                    task.Sync.ServerVersion,
                    task.Sync.DeletedAt), cancellationToken);
            }
        }

        await _repository.SetLastPulledServerVersionAsync(response.ServerVersion, cancellationToken);
    }

    private static string TryReadAssigneeName(IReadOnlyDictionary<string, object?>? roleFields)
        => roleFields is not null && roleFields.TryGetValue("assigneeName", out var value) ? value?.ToString() ?? string.Empty : string.Empty;

    private static ViewModels.TaskUrgencyLevel ToUrgency(HeyeTodo.Shared.Enums.TaskPriority priority)
        => priority switch
        {
            HeyeTodo.Shared.Enums.TaskPriority.Low => ViewModels.TaskUrgencyLevel.Low,
            HeyeTodo.Shared.Enums.TaskPriority.High => ViewModels.TaskUrgencyLevel.High,
            HeyeTodo.Shared.Enums.TaskPriority.Urgent => ViewModels.TaskUrgencyLevel.Urgent,
            _ => ViewModels.TaskUrgencyLevel.Medium,
        };
}
