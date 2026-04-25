using HeyeTodo.Shared.Contracts.Planning;

namespace HeyeTodo.Server.Application.Planning;

public interface IPlanningService
{
    Task<PlanningResponse> PlanAsync(Guid userId, PlanningRequest request, CancellationToken ct = default);
}
