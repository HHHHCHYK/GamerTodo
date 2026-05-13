using System.Net.Http.Headers;
using System.Net.Http.Json;
using HeyeTodo.Shared.Contracts.Auth;
using HeyeTodo.Shared.Contracts.Sync;

namespace HeyeTodo.Client.Services;

public sealed class HeyeTodoApiClient
{
    private static readonly TimeSpan RefreshSkew = TimeSpan.FromMinutes(2);

    private readonly HttpClient _http;
    private readonly IClientSessionStore _sessionStore;

    public HeyeTodoApiClient(HttpClient http, IClientSessionStore sessionStore)
    {
        _http = http;
        _sessionStore = sessionStore;
    }

    public async Task<AuthResponse> RegisterAsync(string serverBaseUrl, RegisterRequest request, CancellationToken cancellationToken = default)
    {
        await _sessionStore.SaveServerBaseUrlAsync(serverBaseUrl, cancellationToken);
        var session = await _sessionStore.LoadAsync(cancellationToken);
        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(serverBaseUrl, "/api/auth/register"))
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Client-Id", session.ClientId.ToString("D"));

        using var response = await _http.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        var auth = await response.Content.ReadFromJsonAsync<AuthResponse>(cancellationToken);
        if (auth is null)
        {
            throw new InvalidOperationException("Register returned an empty response.");
        }

        await _sessionStore.SaveTokensAsync(auth.AccessToken, auth.RefreshToken, auth.ExpiresAt, cancellationToken);
        return auth;
    }

    public async Task<AuthResponse> LoginAsync(string serverBaseUrl, string email, string password, CancellationToken cancellationToken = default)
    {
        await _sessionStore.SaveServerBaseUrlAsync(serverBaseUrl, cancellationToken);
        var session = await _sessionStore.LoadAsync(cancellationToken);
        var request = new LoginRequest(email, password, session.ClientId);
        var auth = await PostAsync<AuthResponse>(session with { ServerBaseUrl = serverBaseUrl }, "/api/auth/login", request, authenticated: false, cancellationToken);
        await _sessionStore.SaveTokensAsync(auth.AccessToken, auth.RefreshToken, auth.ExpiresAt, cancellationToken);
        return auth;
    }

    public async Task<SyncPullResponse> PullAsync(long sinceServerVersion, CancellationToken cancellationToken = default)
    {
        var session = await EnsureAccessTokenAsync(cancellationToken);
        return await GetAsync<SyncPullResponse>(session, $"/api/sync/pull?sinceServerVersion={sinceServerVersion}", cancellationToken);
    }

    public async Task<SyncPushResponse> PushAsync(SyncPushRequest request, CancellationToken cancellationToken = default)
    {
        var session = await EnsureAccessTokenAsync(cancellationToken);
        return await PostAsync<SyncPushResponse>(session, "/api/sync/push", request, authenticated: true, cancellationToken);
    }

    private async Task<ClientSession> EnsureAccessTokenAsync(CancellationToken cancellationToken)
    {
        var session = await _sessionStore.LoadAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(session.ServerBaseUrl))
        {
            throw new InvalidOperationException("Server base URL is not configured.");
        }

        if (string.IsNullOrWhiteSpace(session.AccessToken) || string.IsNullOrWhiteSpace(session.RefreshToken))
        {
            throw new InvalidOperationException("User is not logged in.");
        }

        if (session.AccessTokenExpiresAt is null || session.AccessTokenExpiresAt.Value <= DateTimeOffset.UtcNow.Add(RefreshSkew))
        {
            var refreshed = await PostAsync<AuthResponse>(session, "/api/auth/refresh", new RefreshRequest(session.RefreshToken), authenticated: false, cancellationToken);
            await _sessionStore.SaveTokensAsync(refreshed.AccessToken, refreshed.RefreshToken, refreshed.ExpiresAt, cancellationToken);
            session = session with
            {
                AccessToken = refreshed.AccessToken,
                RefreshToken = refreshed.RefreshToken,
                AccessTokenExpiresAt = refreshed.ExpiresAt
            };
        }

        return session;
    }

    private async Task<T> GetAsync<T>(ClientSession session, string pathAndQuery, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Get, BuildUri(session.ServerBaseUrl!, pathAndQuery));
        ApplyAuthHeaders(message, session);
        using var response = await _http.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private async Task<T> PostAsync<T>(ClientSession session, string path, object request, bool authenticated, CancellationToken cancellationToken)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, BuildUri(session.ServerBaseUrl!, path))
        {
            Content = JsonContent.Create(request)
        };
        message.Headers.Add("X-Client-Id", session.ClientId.ToString("D"));
        if (authenticated)
        {
            ApplyAuthHeaders(message, session);
        }

        using var response = await _http.SendAsync(message, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await ReadResponseAsync<T>(response, cancellationToken);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        var value = await response.Content.ReadFromJsonAsync<T>(cancellationToken);
        return value ?? throw new InvalidOperationException($"Server returned an empty {typeof(T).Name} response.");
    }

    private static void ApplyAuthHeaders(HttpRequestMessage message, ClientSession session)
    {
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
        message.Headers.Add("X-Client-Id", session.ClientId.ToString("D"));
    }

    private static Uri BuildUri(string serverBaseUrl, string pathAndQuery)
        => new(new Uri(serverBaseUrl.Trim().TrimEnd('/') + "/"), pathAndQuery.TrimStart('/'));
}
