using HeyeTodo.Shared.Contracts.Planning;

namespace HeyeTodo.Server.Application.Planning;

public interface IPlanningLlmDriver
{
    string Name { get; }
    bool IsConfigured { get; }
    Task<PlanningResponse> PlanAsync(PlanningRequest request, CancellationToken ct = default);
}
