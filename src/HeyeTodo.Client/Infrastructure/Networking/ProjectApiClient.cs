using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Infrastructure.Networking;

public sealed class ProjectApiClient
{
    private readonly ApiClient _api;

    public ProjectApiClient(ApiClient api)
    {
        _api = api;
    }

    public async Task<IReadOnlyList<ProjectDto>?> GetProjectsAsync(CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/projects");
        using var response = await _api.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
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
            return null;
        }

        return await response.Content.ReadFromJsonAsync<ProjectDto>(ct);
    }

    public async Task<bool> DeleteProjectAsync(Guid projectId, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/projects/{projectId:D}");
        using var response = await _api.SendAsync(message, ct);
        return response.IsSuccessStatusCode;
    }
}
