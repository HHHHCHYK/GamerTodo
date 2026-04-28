using System.Net.Http;
using System.Net.Http.Json;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Sync;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SyncApiClient
{
    private readonly ApiClient _api;
    private readonly IClientLogger _logger;

    public SyncApiClient(ApiClient api, IClientLogger logger)
    {
        _api = api;
        _logger = logger;
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
            await _logger.LogSyncOperationAsync("PushHttp", ClientLogLevel.Warning, "Push HTTP request returned a non-success status code.", new Dictionary<string, object?>
            {
                ["statusCode"] = (int)response.StatusCode,
                ["reasonPhrase"] = response.ReasonPhrase,
                ["changeCount"] = request.Changes.Count,
            }, ct: ct);
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
            await _logger.LogSyncOperationAsync("PullHttp", ClientLogLevel.Warning, "Pull HTTP request returned a non-success status code.", new Dictionary<string, object?>
            {
                ["statusCode"] = (int)response.StatusCode,
                ["reasonPhrase"] = response.ReasonPhrase,
                ["sinceServerVersion"] = sinceServerVersion,
            }, ct: ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<SyncPullResponse>(ct);
    }
}
