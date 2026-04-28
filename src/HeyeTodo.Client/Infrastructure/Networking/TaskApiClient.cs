using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Infrastructure.Networking;

public sealed class TaskApiClient
{
    private readonly ApiClient _api;
    private readonly IClientLogger _logger;

    public TaskApiClient(ApiClient api, IClientLogger logger)
    {
        _api = api;
        _logger = logger;
    }

    public async Task<IReadOnlyList<TaskDto>?> GetTasksAsync(TaskListQuery query, CancellationToken ct = default)
    {
        var uri = BuildQueryUri(query);
        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        using var response = await _api.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("GetTasks", response, query.ProjectId, null, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<List<TaskDto>>(ct);
    }

    public async Task<TaskDto?> GetTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/tasks/{taskId:D}");
        using var response = await _api.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("GetTask", response, null, taskId, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TaskDto>(ct);
    }

    public async Task<TaskDto?> CreateTaskAsync(CreateTaskRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Post, "/api/tasks")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("CreateTask", response, request.ProjectId, null, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TaskDto>(ct);
    }

    public async Task<TaskDto?> UpdateTaskAsync(Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{taskId:D}")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("UpdateTask", response, request.ProjectId, taskId, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TaskDto>(ct);
    }

    public async Task<TaskDto?> ChangeTaskStatusAsync(Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Patch, $"/api/tasks/{taskId:D}/status")
        {
            Content = JsonContent.Create(request),
        };
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("ChangeTaskStatus", response, null, taskId, ct);
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TaskDto>(ct);
    }

    public async Task<bool> DeleteTaskAsync(Guid taskId, CancellationToken ct = default)
    {
        using var message = new HttpRequestMessage(HttpMethod.Delete, $"/api/tasks/{taskId:D}");
        using var response = await _api.SendAsync(message, ct);
        if (!response.IsSuccessStatusCode)
        {
            await LogFailureAsync("DeleteTask", response, null, taskId, ct);
        }

        return response.IsSuccessStatusCode;
    }

    private Task LogFailureAsync(string operation, HttpResponseMessage response, Guid? projectId, Guid? taskId, CancellationToken ct)
        => _logger.LogOperationAsync("TaskApi", operation, ClientLogLevel.Warning, "Task API request failed.", new Dictionary<string, object?>
        {
            ["statusCode"] = (int)response.StatusCode,
            ["reasonPhrase"] = response.ReasonPhrase,
            ["projectId"] = projectId,
            ["taskId"] = taskId,
        }, ct: ct);

    private static string BuildQueryUri(TaskListQuery query)
    {
        var parts = new List<string>();
        if (query.ProjectId is not null)
        {
            parts.Add($"ProjectId={query.ProjectId:D}");
        }

        if (query.Status is not null)
        {
            parts.Add($"Status={(int)query.Status.Value}");
        }

        if (query.Priority is not null)
        {
            parts.Add($"Priority={(int)query.Priority.Value}");
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            parts.Add($"Search={Uri.EscapeDataString(query.Search)}");
        }

        parts.Add($"SortBy={(int)query.SortBy}");
        parts.Add($"SortDirection={(int)query.SortDirection}");
        parts.Add($"IncludeCompleted={query.IncludeCompleted.ToString().ToLowerInvariant()}");

        return parts.Count == 0 ? "/api/tasks" : $"/api/tasks?{string.Join("&", parts)}";
    }
}
