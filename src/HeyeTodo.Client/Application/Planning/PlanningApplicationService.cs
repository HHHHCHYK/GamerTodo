using System.Text.Json;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Shared.Contracts.Planning;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Client.Application.Planning;

public interface IPlanningApplicationService
{
    Task<PlanningResponse> PlanAsync(Guid ownerId, Guid? projectId, string? prompt = null, CancellationToken ct = default);
}

public sealed class PlanningApplicationService : IPlanningApplicationService
{
    private readonly ITaskRepository _tasks;
    private readonly IDependencyRepository _dependencies;
    private readonly ISettingsService _settings;
    private readonly IEnumerable<IPlanningDriver> _drivers;

    public PlanningApplicationService(ITaskRepository tasks, IDependencyRepository dependencies, ISettingsService settings, IEnumerable<IPlanningDriver> drivers)
    {
        _tasks = tasks;
        _dependencies = dependencies;
        _settings = settings;
        _drivers = drivers;
    }

    public async Task<PlanningResponse> PlanAsync(Guid ownerId, Guid? projectId, string? prompt = null, CancellationToken ct = default)
    {
        var settings = _settings.Current;
        var query = new TaskListQuery(projectId, null, null, null, TaskSortField.Priority, SortDirection.Descending, true);
        var tasks = await _tasks.ListAsync(ownerId, query, ct);
        var dependencies = await _dependencies.ListAsync(ownerId, projectId, ct);
        var request = new PlanningRequest(
            projectId,
            settings.PlanningMode,
            DateTimeOffset.UtcNow.Date,
            tasks.Select(x => new PlanningTaskInput(
                x.Id,
                x.ProjectId,
                x.Title,
                x.Description,
                x.Status,
                x.Priority,
                x.StartDate,
                x.EndDate,
                x.EstimatedHours,
                ReadRoleFields(x.RoleFieldsJson))).ToList(),
            dependencies.Select(x => new PlanningDependencyInput(x.Id, x.ProjectId, x.PredecessorId, x.SuccessorId, x.Type)).ToList(),
            prompt);

        var driver = _drivers.FirstOrDefault(x => string.Equals(x.Name, settings.PlanningMode, StringComparison.OrdinalIgnoreCase))
                     ?? _drivers.First(x => x.Name == "rule");
        return await driver.PlanAsync(request, settings, ct);
    }

    private static IReadOnlyDictionary<string, object?>? ReadRoleFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
