using HeyeTodo.Server.Application.Common;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Server.Application.Projects;

public interface IProjectService
{
    Task<ServiceResult<IReadOnlyList<ProjectDto>>> ListAsync(Guid userId, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> CreateAsync(Guid userId, Guid clientId, CreateProjectRequest request, CancellationToken ct = default);
    Task<ServiceResult<ProjectDto>> UpdateAsync(Guid userId, Guid clientId, Guid projectId, UpdateProjectRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(Guid userId, Guid clientId, Guid projectId, CancellationToken ct = default);
}
