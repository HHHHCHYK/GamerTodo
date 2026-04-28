using System.Collections.Generic;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Networking;
using Microsoft.AspNetCore.SignalR.Client;

namespace HeyeTodo.Client.Application.Sync;

public sealed class SignalRSyncClient : IAsyncDisposable
{
    private readonly ApiClient _api;
    private readonly ISettingsService _settings;
    private readonly IClientLogger _logger;
    private HubConnection? _connection;

    public SignalRSyncClient(ApiClient api, ISettingsService settings, IClientLogger logger)
    {
        _api = api;
        _settings = settings;
        _logger = logger;
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
            await _logger.LogSyncOperationAsync("SignalRReconnect", ClientLogLevel.Information, "SignalR sync connection restarted.", new Dictionary<string, object?>
            {
                ["state"] = _connection.State,
            }, ct: ct);
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

        _connection.Reconnecting += async error =>
        {
            await _logger.LogSyncOperationAsync("SignalRReconnecting", ClientLogLevel.Warning, "SignalR sync connection is reconnecting.", null, error);
        };
        _connection.Reconnected += async connectionId =>
        {
            await _logger.LogSyncOperationAsync("SignalRReconnected", ClientLogLevel.Information, "SignalR sync connection reconnected.", new Dictionary<string, object?>
            {
                ["connectionId"] = connectionId,
            });
        };
        _connection.Closed += async error =>
        {
            await _logger.LogSyncOperationAsync("SignalRClosed", error is null ? ClientLogLevel.Information : ClientLogLevel.Warning, "SignalR sync connection closed.", null, error);
        };

        _connection.On<Guid>("ProjectInvalidated", projectId =>
        {
            _ = _logger.LogSyncOperationAsync("SignalRProjectInvalidated", ClientLogLevel.Information, "Project invalidation received.", new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
            });
            ProjectInvalidated?.Invoke(projectId);
        });
        await _connection.StartAsync(ct);
        await _logger.LogSyncOperationAsync("SignalRConnect", ClientLogLevel.Information, "SignalR sync connection started.", new Dictionary<string, object?>
        {
            ["hubPath"] = hubUri.AbsolutePath,
            ["state"] = _connection.State,
        }, ct: ct);
    }

    public async Task SubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        await EnsureConnectedAsync(ct);
        if (_connection is not null)
        {
            await _connection.InvokeAsync("SubscribeProject", projectId, ct);
            await _logger.LogSyncOperationAsync("SignalRSubscribeProject", ClientLogLevel.Information, "Project subscribed on SignalR sync hub.", new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
            }, ct: ct);
        }
    }

    public async Task UnsubscribeProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await _connection.InvokeAsync("UnsubscribeProject", projectId, ct);
            await _logger.LogSyncOperationAsync("SignalRUnsubscribeProject", ClientLogLevel.Information, "Project unsubscribed from SignalR sync hub.", new Dictionary<string, object?>
            {
                ["projectId"] = projectId,
            }, ct: ct);
        }
    }

    public async Task StopAsync(CancellationToken ct = default)
    {
        if (_connection is not null)
        {
            await _connection.StopAsync(ct);
            await _logger.LogSyncOperationAsync("SignalRStop", ClientLogLevel.Information, "SignalR sync connection stopped.", ct: ct);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
            await _logger.LogSyncOperationAsync("SignalRDispose", ClientLogLevel.Information, "SignalR sync connection disposed.");
        }
    }
}
