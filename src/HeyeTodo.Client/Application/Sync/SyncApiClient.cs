using System.Net.Http;
using System.Net.Http.Json;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Sync;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SyncApiClient
{
    private readonly ApiClient _api;

    public SyncApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<SyncPushResponse?> PushAsync(SyncPushRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/sync/push")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SyncPushResponse>(ct);
    }

    public async Task<SyncPullResponse?> PullAsync(long sinceServerVersion, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, $"/api/sync/pull?sinceServerVersion={sinceServerVersion}");
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SyncPullResponse>(ct);
    }
}
