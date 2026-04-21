using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Infrastructure.Auth;
using HeyeTodo.Shared.Contracts.Auth;

namespace HeyeTodo.Client.Infrastructure.Networking;

/// <summary>
/// Thin HTTP wrapper around the Server REST API.
/// Injects the bearer token from <see cref="TokenStore"/> and auto-refreshes on 401.
/// </summary>
public sealed class ApiClient
{
    private readonly HttpClient _http;
    private readonly TokenStore _tokens;
    private readonly Guid _clientId;
    private TokenBundle? _current;

    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public ApiClient(HttpClient http, TokenStore tokens, Guid clientId)
    {
        _http = http;
        _tokens = tokens;
        _clientId = clientId;
        _current = _tokens.Load();
        _http.DefaultRequestHeaders.Add("X-Client-Id", clientId.ToString("D"));
    }

    public TokenBundle? CurrentTokens => _current;
    public bool IsAuthenticated => _current is not null;

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync("/api/auth/register", req, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (auth is not null) CommitTokens(auth);
        return auth;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var req = new LoginRequest(email, password, _clientId);
        using var resp = await _http.PostAsJsonAsync("/api/auth/login", req, ct);
        password = string.Empty;
        if (!resp.IsSuccessStatusCode) return null;
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (auth is not null) CommitTokens(auth);
        return auth;
    }

    public void Logout()
    {
        _current = null;
        _tokens.Save(null);
    }

    /// <summary>Send an authenticated request; refresh + retry once on 401.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        Attach(request);
        var resp = await _http.SendAsync(request, ct);
        if (resp.StatusCode != System.Net.HttpStatusCode.Unauthorized) return resp;

        resp.Dispose();
        if (!await TryRefreshAsync(ct)) return new HttpResponseMessage(System.Net.HttpStatusCode.Unauthorized);

        var retry = await request.CloneAsync();
        Attach(retry);
        return await _http.SendAsync(retry, ct);
    }

    private void CommitTokens(AuthResponse r)
    {
        _current = new TokenBundle(r.AccessToken, r.RefreshToken, r.AccessTokenExpiresAt, r.User.Id, r.User.Email);
        _tokens.Save(_current);
    }

    private void Attach(HttpRequestMessage msg)
    {
        if (_current is not null)
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _current.AccessToken);
    }

    private async Task<bool> TryRefreshAsync(CancellationToken ct)
    {
        if (_current is null) return false;
        await RefreshGate.WaitAsync(ct);
        try
        {
            using var resp = await _http.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(_current.RefreshToken), ct);
            if (!resp.IsSuccessStatusCode) { Logout(); return false; }
            var r = await resp.Content.ReadFromJsonAsync<AuthResponse>(ct);
            if (r is null) { Logout(); return false; }
            CommitTokens(r);
            return true;
        }
        finally { RefreshGate.Release(); }
    }
}

internal static class HttpRequestMessageExtensions
{
    public static async Task<HttpRequestMessage> CloneAsync(this HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri) { Version = req.Version };
        if (req.Content is not null)
        {
            var ms = new System.IO.MemoryStream();
            await req.Content.CopyToAsync(ms);
            ms.Position = 0;
            clone.Content = new StreamContent(ms);
            foreach (var h in req.Content.Headers)
                clone.Content.Headers.TryAddWithoutValidation(h.Key, h.Value);
        }
        foreach (var h in req.Headers)
            clone.Headers.TryAddWithoutValidation(h.Key, h.Value);
        foreach (var p in req.Options)
            ((IDictionary<string, object?>)clone.Options).TryAdd(p.Key, p.Value);
        return clone;
    }
}
