using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Infrastructure.Auth;
using HeyeTodo.Client.Infrastructure.Logging;
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
    private readonly ISettingsService _settings;
    private readonly IClientLogger _logger;
    private readonly Guid _clientId;
    private TokenBundle? _current;

    private static readonly SemaphoreSlim RefreshGate = new(1, 1);

    public ApiClient(HttpClient http, TokenStore tokens, ISettingsService settings, IClientLogger logger, Guid clientId)
    {
        _http = http;
        _tokens = tokens;
        _settings = settings;
        _logger = logger;
        _clientId = clientId;
        _current = _tokens.Load();
        _http.DefaultRequestHeaders.Add("X-Client-Id", clientId.ToString("D"));
    }

    public TokenBundle? CurrentTokens => _current;
    public bool IsAuthenticated => _current is not null;

    public async Task<AuthResponse?> RegisterAsync(RegisterRequest req, CancellationToken ct = default)
    {
        using var resp = await _http.PostAsJsonAsync(BuildUri("/api/auth/register"), req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            await LogHttpFailureAsync("Register", resp, ct);
            return null;
        }

        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (auth is not null)
        {
            CommitTokens(auth);
            await _logger.LogOperationAsync("ApiClient", "Register", ClientLogLevel.Information, "Registration tokens committed.", new Dictionary<string, object?>
            {
                ["userId"] = auth.User.Id,
            }, ct: ct);
        }

        return auth;
    }

    public async Task<AuthResponse?> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        var req = new LoginRequest(email, password, _clientId);
        using var resp = await _http.PostAsJsonAsync(BuildUri("/api/auth/login"), req, ct);
        password = string.Empty;
        if (!resp.IsSuccessStatusCode)
        {
            await LogHttpFailureAsync("Login", resp, ct);
            return null;
        }

        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(ct);
        if (auth is not null)
        {
            CommitTokens(auth);
            await _logger.LogOperationAsync("ApiClient", "Login", ClientLogLevel.Information, "Login tokens committed.", new Dictionary<string, object?>
            {
                ["userId"] = auth.User.Id,
            }, ct: ct);
        }

        return auth;
    }

    public async Task<UserDto?> GetCurrentUserAsync(CancellationToken ct = default)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, BuildUri("/api/users/me"));
        using var resp = await SendAsync(req, ct);
        if (!resp.IsSuccessStatusCode)
        {
            await LogHttpFailureAsync("GetCurrentUser", resp, ct);
            return null;
        }

        return await resp.Content.ReadFromJsonAsync<UserDto>(ct);
    }

    public async Task<UserDto?> UpdateRolesAsync(UpdateRolesRequest req, CancellationToken ct = default)
    {
        using var msg = new HttpRequestMessage(HttpMethod.Patch, BuildUri("/api/users/me/roles"))
        {
            Content = JsonContent.Create(req),
        };
        using var resp = await SendAsync(msg, ct);
        if (!resp.IsSuccessStatusCode)
        {
            await LogHttpFailureAsync("UpdateRoles", resp, ct);
            return null;
        }

        return await resp.Content.ReadFromJsonAsync<UserDto>(ct);
    }

    public async Task LogoutAsync(CancellationToken ct = default)
    {
        if (_current is not null)
        {
            using var resp = await _http.PostAsJsonAsync(BuildUri("/api/auth/logout"), new RefreshRequest(_current.RefreshToken), ct);
            if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Unauthorized)
            {
                await LogHttpFailureAsync("Logout", resp, ct);
            }

            if (resp.StatusCode == HttpStatusCode.Unauthorized)
            {
                _current = null;
                _tokens.Save(null);
                await _logger.LogOperationAsync("ApiClient", "Logout", ClientLogLevel.Information, "Local tokens cleared after unauthorized logout response.", ct: ct);
                return;
            }
        }

        _current = null;
        _tokens.Save(null);
        await _logger.LogOperationAsync("ApiClient", "Logout", ClientLogLevel.Information, "Local tokens cleared.", ct: ct);
    }

    /// <summary>Send an authenticated request; refresh + retry once on 401.</summary>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct = default)
    {
        if (request.RequestUri is null)
        {
            throw new InvalidOperationException("Request URI is required.");
        }

        if (!request.RequestUri.IsAbsoluteUri)
        {
            request.RequestUri = BuildUri(request.RequestUri.ToString());
        }

        Attach(request);
        var resp = await _http.SendAsync(request, ct);
        if (resp.StatusCode != HttpStatusCode.Unauthorized)
        {
            return resp;
        }

        await _logger.LogOperationAsync("ApiClient", "Unauthorized", ClientLogLevel.Warning, "Authenticated request returned unauthorized; trying refresh.", new Dictionary<string, object?>
        {
            ["method"] = request.Method.Method,
            ["path"] = request.RequestUri.AbsolutePath,
        }, ct: ct);
        resp.Dispose();
        if (!await TryRefreshAsync(ct))
        {
            await _logger.LogOperationAsync("ApiClient", "Refresh", ClientLogLevel.Warning, "Token refresh failed; returning unauthorized response.", ct: ct);
            return new HttpResponseMessage(HttpStatusCode.Unauthorized);
        }

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
        if (_current is null)
        {
            return false;
        }

        await RefreshGate.WaitAsync(ct);
        try
        {
            using var resp = await _http.PostAsJsonAsync(BuildUri("/api/auth/refresh"), new RefreshRequest(_current.RefreshToken), ct);
            if (!resp.IsSuccessStatusCode)
            {
                await LogHttpFailureAsync("Refresh", resp, ct);
                await LogoutAsync(ct);
                return false;
            }

            var r = await resp.Content.ReadFromJsonAsync<AuthResponse>(ct);
            if (r is null)
            {
                await _logger.LogOperationAsync("ApiClient", "Refresh", ClientLogLevel.Warning, "Token refresh returned an empty response.", ct: ct);
                await LogoutAsync(ct);
                return false;
            }

            CommitTokens(r);
            await _logger.LogOperationAsync("ApiClient", "Refresh", ClientLogLevel.Information, "Token refresh completed.", new Dictionary<string, object?>
            {
                ["userId"] = r.User.Id,
            }, ct: ct);
            return true;
        }
        finally
        {
            RefreshGate.Release();
        }
    }

    private Task LogHttpFailureAsync(string operation, HttpResponseMessage response, CancellationToken ct)
        => _logger.LogOperationAsync("ApiClient", operation, ClientLogLevel.Warning, "HTTP request failed.", new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["reasonPhrase"] = response.ReasonPhrase,
        }, ct: ct);

    private Uri BuildUri(string relativePath)
    {
        var baseUri = new Uri(_settings.Current.ServerBaseUrl, UriKind.Absolute);
        return new Uri(baseUri, relativePath);
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
