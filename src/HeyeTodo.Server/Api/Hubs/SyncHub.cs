using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace HeyeTodo.Server.Api.Hubs;

/// <summary>
/// SignalR hub that broadcasts sync change envelopes to subscribed clients.
/// Each client joins a group per project they care about (<c>project:{id}</c>).
/// Actual change delivery will be wired up in M3 (Sync Engine milestone).
/// </summary>
[Authorize]
public sealed class SyncHub : Hub
{
    public const string ProjectInvalidatedMethod = "ProjectInvalidated";

    public Task SubscribeProject(Guid projectId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));

    public Task UnsubscribeProject(Guid projectId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));

    public static string GroupName(Guid projectId) => $"project:{projectId:N}";
}
