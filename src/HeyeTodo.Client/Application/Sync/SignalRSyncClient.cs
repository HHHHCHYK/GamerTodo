using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Auth;
using HeyeTodo.Client.Infrastructure.Networking;
using Microsoft.AspNetCore.SignalR.Client;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SignalRSyncClient : IAsyncDisposable
{
    private readonly ApiClient _api;
    private readonly ISettingsService _settings;
    private HubConnection? _connection;

    public SignalRSyncClient(ApiClient api, ISettingsService settings)
    {
        _api = api;
        _settings = settings;
    }

    public event Action<Guid>? ProjectInvalidated;

    public async Task EnsureConnectedAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            if (_connection.State == HubConnectionState.Connected)
            {
                return;
            }

            await _connection.StartAsync(ct);
            return;
        }

        var baseUri = new Uri(_settings.Current.ServerBaseUrl, UriKind.Absolute);
        var hubUri = new Uri(baseUri, "/ws/sync");

        _connection = new HubConnectionBuilder()
            .WithUrl(hubUri, options =>
            {
                options.AccessTokenProvider = () => Task.FromResult(_api.CurrentTokens?.AccessToken);
            })
            .WithAutomaticReconnect()
            .Build();

        _connection.On<Guid>("ProjectInvalidated", projectId => ProjectInvalidated?.Invoke(projectId));
        await _connection.StartAsync(ct);
    }

    public async Task SubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        if (_connection is not null)
        {
            await _connection.InvokeAsync("SubscribeProject", projectId, ct);
        }
    }

    public async Task UnsubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await _connection.InvokeAsync("UnsubscribeProject", projectId, ct);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }
    }
}
