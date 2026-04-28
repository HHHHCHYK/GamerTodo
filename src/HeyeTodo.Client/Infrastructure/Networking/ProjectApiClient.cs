using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Infrastructure.Networking;

public sealed class ProjectApiClient
{
    private readonly ApiClient _api;
    private readonly IClientLogger _logger;

    public ProjectApiClient(ApiClient api, IClientLogger logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ProjectDto>?> GetProjectsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        using var response = await _api.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("GetProjects", response, null, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<ProjectDto>>(ct);
    }

    public async Task<ProjectDto?> CreateProjectAsync(CreateProjectRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/projects")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("CreateProject", response, null, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProjectDto>(ct);
    }

    public async Task<ProjectDto?> UpdateProjectAsync(Guid projectId, UpdateProjectRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/projects/{projectId:D}")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("UpdateProject", response, projectId, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProjectDto>(ct);
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId:D}");
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("DeleteProject", response, projectId, ct);
        }

        return response.IsSuccessStatusCode;
    }

    private Task LogFailureAsync(string operation, HttpResponseMessage response, Guid? projectId, CancellationToken ct)
        => _logger.LogOperationAsync("ProjectApi", operation, ClientLogLevel.Warning, "Project API request failed.", new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["reasonPhrase"] = response.ReasonPhrase,
            ["projectId"] = projectId,
        }, ct: ct);
}
