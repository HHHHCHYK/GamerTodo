using HeyeTodo.Client.Persistence;

namespace HeyeTodo.Client.Services;

public sealed class ClientSessionStore : IClientSessionStore
{
    private const string ClientIdKey = "client.id";
    private const string ServerBaseUrlKey = "server.baseUrl";
    private const string AccessTokenKey = "auth.accessToken";
    private const string RefreshTokenKey = "auth.refreshToken";
    private const string AccessTokenExpiresAtKey = "auth.accessTokenExpiresAt";

    private readonly ITaskRepository _repository;

    public ClientSessionStore(ITaskRepository repository)
    {
        _repository = repository;
    }

    public async Task<ClientSession> LoadAsync(CancellationToken cancellationToken = default)
    {
        var clientIdText = await _repository.GetSettingAsync(ClientIdKey, cancellationToken);
        if (!Guid.TryParse(clientIdText, out var clientId))
        {
            clientId = Guid.NewGuid();
            await _repository.SetSettingAsync(ClientIdKey, clientId.ToString("D"), cancellationToken);
        }

        var expiresText = await _repository.GetSettingAsync(AccessTokenExpiresAtKey, cancellationToken);
        DateTimeOffset? expiresAt = DateTimeOffset.TryParse(expiresText, out var parsed) ? parsed : null;

        return new ClientSession(
            clientId,
            await _repository.GetSettingAsync(ServerBaseUrlKey, cancellationToken),
            await _repository.GetSettingAsync(AccessTokenKey, cancellationToken),
            await _repository.GetSettingAsync(RefreshTokenKey, cancellationToken),
            expiresAt);
    }

    public Task SaveServerBaseUrlAsync(string serverBaseUrl, CancellationToken cancellationToken = default)
        => _repository.SetSettingAsync(ServerBaseUrlKey, serverBaseUrl.Trim().TrimEnd('/'), cancellationToken);

    public async Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default)
    {
        await _repository.SetSettingAsync(AccessTokenKey, accessToken, cancellationToken);
        await _repository.SetSettingAsync(RefreshTokenKey, refreshToken, cancellationToken);
        await _repository.SetSettingAsync(AccessTokenExpiresAtKey, expiresAt.ToString("O"), cancellationToken);
    }

    public async Task ClearTokensAsync(CancellationToken cancellationToken = default)
    {
        await _repository.SetSettingAsync(AccessTokenKey, string.Empty, cancellationToken);
        await _repository.SetSettingAsync(RefreshTokenKey, string.Empty, cancellationToken);
        await _repository.SetSettingAsync(AccessTokenExpiresAtKey, string.Empty, cancellationToken);
    }
}
