namespace HeyeTodo.Client.Services;

public interface IClientSessionStore
{
    Task<ClientSession> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveServerBaseUrlAsync(string serverBaseUrl, CancellationToken cancellationToken = default);

    Task SaveTokensAsync(string accessToken, string refreshToken, DateTimeOffset expiresAt, CancellationToken cancellationToken = default);

    Task ClearTokensAsync(CancellationToken cancellationToken = default);
}
