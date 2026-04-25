using System.Net.Http.Json;
using System.Text.Json;
using HeyeTodo.Shared.Contracts.Planning;
using HeyeTodo.Shared.Planning;
using Microsoft.Extensions.Options;

namespace HeyeTodo.Server.Application.Planning;

public sealed class ServerPlanningOptions
{
    public string? Endpoint { get; set; }
    public string? ApiKey { get; set; }
    public string Model { get; set; } = "gpt-4o-mini";
}

public sealed class ServerProxyLlmDriver : IPlanningLlmDriver
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient _http;
    private readonly ServerPlanningOptions _options;

    public ServerProxyLlmDriver(HttpClient http, IOptions<ServerPlanningOptions> options)
    {
        _http = http;
        _options = options.Value;
    }

    public string Name => "server";
    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.Endpoint) && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public async Task<PlanningResponse> PlanAsync(PlanningRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            var fallback = RuleBasedPlanner.Plan(request);
            return fallback with
            {
                Driver = "server-fallback-rule",
                Issues = fallback.Issues.Concat([new PlanningIssue("ServerLlmNotConfigured", "Server proxy LLM is not configured; rule-based planning was used instead.")]).ToList(),
            };
        }

        using var message = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint)
        {
            Content = JsonContent.Create(new
            {
                model = _options.Model,
                prompt = request.Prompt,
                tasks = request.Tasks,
                dependencies = request.Dependencies,
                anchorDate = request.AnchorDate,
            }, options: JsonOptions),
        };
        message.Headers.TryAddWithoutValidation("Authorization", $"Bearer {_options.ApiKey}");

        using var response = await _http.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            var fallback = RuleBasedPlanner.Plan(request);
            return fallback with
            {
                Driver = "server-fallback-rule",
                Issues = fallback.Issues.Concat([new PlanningIssue("ServerLlmRequestFailed", $"Server proxy LLM returned HTTP {(int)response.StatusCode}; rule-based planning was used instead.")]).ToList(),
            };
        }

        var result = await response.Content.ReadFromJsonAsync<PlanningResponse>(JsonOptions, ct);
        return result ?? RuleBasedPlanner.Plan(request) with
        {
            Driver = "server-fallback-rule",
            Issues = [new PlanningIssue("ServerLlmEmptyResponse", "Server proxy LLM returned an empty response; rule-based planning was used instead.")],
        };
    }
}
