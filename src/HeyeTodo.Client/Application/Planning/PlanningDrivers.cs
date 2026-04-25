using System.Net.Http.Json;
using System.Text.Json;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Contracts.Planning;
using HeyeTodo.Shared.Planning;

namespace HeyeTodo.Client.Application.Planning;

public interface IPlanningDriver
{
    string Name { get; }
    bool CanRun(AppSettings settings);
    Task<PlanningResponse> PlanAsync(PlanningRequest request, AppSettings settings, CancellationToken ct = default);
}

public sealed class RulePlanningDriver : IPlanningDriver
{
    public string Name => "rule";
    public bool CanRun(AppSettings settings) => true;
    public Task<PlanningResponse> PlanAsync(PlanningRequest request, AppSettings settings, CancellationToken ct = default)
        => Task.FromResult(RuleBasedPlanner.Plan(request));
}

public sealed class ServerProxyPlanningDriver : IPlanningDriver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ApiClient _api;

    public ServerProxyPlanningDriver(ApiClient api)
    {
        _api = api;
    }

    public string Name => "server";
    public bool CanRun(AppSettings settings) => true;

    public async Task<PlanningResponse> PlanAsync(PlanningRequest request, AppSettings settings, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, new Uri(new Uri(settings.ServerBaseUrl, UriKind.Absolute), "/api/planning/plan"))
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var fallback = RuleBasedPlanner.Plan(request);
            return fallback with
            {
                Driver = "server-fallback-rule",
                Issues = fallback.Issues.Concat([new PlanningIssue("ServerPlanningRequestFailed", $"Server planning returned HTTP {(int)response.StatusCode}; rule-based planning was used instead.")]).ToList(),
            };
        }

        var result = await response.Content.ReadFromJsonAsync<PlanningResponse>(JsonOptions, ct);
        return result ?? RuleBasedPlanner.Plan(request) with
        {
            Driver = "server-empty-fallback-rule",
            Issues = [new PlanningIssue("ServerPlanningEmptyResponse", "Server planning returned an empty response; rule-based planning was used instead.")],
        };
    }
}

public sealed class ClientKeyPlanningDriver : IPlanningDriver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;

    public ClientKeyPlanningDriver(HttpClient http)
    {
        _http = http;
    }

    public string Name => "client";
    public bool CanRun(AppSettings settings)
        => !string.IsNullOrWhiteSpace(settings.LocalLlmEndpoint) && !string.IsNullOrWhiteSpace(settings.LocalLlmApiKey);

    public async Task<PlanningResponse> PlanAsync(PlanningRequest request, AppSettings settings, CancellationToken ct = default)
    {
        if (!CanRun(settings))
        {
            var fallback = RuleBasedPlanner.Plan(request);
            return fallback with
            {
                Driver = "client-fallback-rule",
                Issues = fallback.Issues.Concat([new PlanningIssue("ClientLlmNotConfigured", "Client-key LLM is not configured; rule-based planning was used instead.")]).ToList(),
            };
        }

        if (!Uri.TryCreate(settings.LocalLlmEndpoint, UriKind.Absolute, out var endpoint))
        {
            var fallback = RuleBasedPlanner.Plan(request);
            return fallback with
            {
                Driver = "client-fallback-rule",
                Issues = fallback.Issues.Concat([new PlanningIssue("ClientLlmInvalidEndpoint", "Client-key LLM endpoint is not a valid absolute URI; rule-based planning was used instead.")]).ToList(),
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {settings.LocalLlmApiKey}");

        using var response = await _http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var fallback = RuleBasedPlanner.Plan(request);
            return fallback with
            {
                Driver = "client-fallback-rule",
                Issues = fallback.Issues.Concat([new PlanningIssue("ClientLlmRequestFailed", $"Client-key LLM returned HTTP {(int)response.StatusCode}; rule-based planning was used instead.")]).ToList(),
            };
        }

        var result = await response.Content.ReadFromJsonAsync<PlanningResponse>(JsonOptions, ct);
        return result ?? RuleBasedPlanner.Plan(request) with
        {
            Driver = "client-fallback-rule",
            Issues = [new PlanningIssue("ClientLlmEmptyResponse", "Client-key LLM returned an empty response; rule-based planning was used instead.")],
        };
    }
}
