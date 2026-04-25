using HeyeTodo.Server.Infrastructure.Persistence;
using HeyeTodo.Shared.Contracts.Planning;
using HeyeTodo.Shared.Planning;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Server.Application.Planning;

public sealed class PlanningService : IPlanningService
{
    private readonly AppDbContext _db;
    private readonly IPlanningLlmDriver _serverDriver;

    public PlanningService(AppDbContext db, IPlanningLlmDriver serverDriver)
    {
        _db = db;
        _serverDriver = serverDriver;
    }

    public async Task<PlanningResponse> PlanAsync(Guid userId, PlanningRequest request, CancellationToken ct = default)
    {
        var projectIds = await _db.Projects
            .Where(x => x.OwnerId == userId && x.DeletedAt == null)
            .Select(x => x.Id)
            .ToListAsync(ct);
        if (request.ProjectId is not null && !projectIds.Contains(request.ProjectId.Value))
        {
            return new PlanningResponse("server", "Project was not found.", [], [new PlanningIssue("ProjectNotFound", "The requested project was not found.")]);
        }

        var allowedProjectIds = request.ProjectId is null ? projectIds.ToHashSet() : new HashSet<Guid> { request.ProjectId.Value };
        var filtered = request with
        {
            Tasks = request.Tasks.Where(x => allowedProjectIds.Contains(x.ProjectId)).ToList(),
            Dependencies = request.Dependencies.Where(x => allowedProjectIds.Contains(x.ProjectId)).ToList(),
        };

        if (string.Equals(request.Mode, "server", StringComparison.OrdinalIgnoreCase))
        {
            return await _serverDriver.PlanAsync(filtered, ct);
        }

        return RuleBasedPlanner.Plan(filtered);
    }
}
