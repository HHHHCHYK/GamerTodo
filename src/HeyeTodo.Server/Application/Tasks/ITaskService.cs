using HeyeTodo.Server.Application.Common;
using HeyeTodo.Shared.Contracts.Tasks;

namespace HeyeTodo.Server.Application.Tasks;

public interface ITaskService
{
    Task<ServiceResult<IReadOnlyList<TaskDto>>> ListAsync(Guid userId, TaskListQuery query, CancellationToken ct = default);
    Task<ServiceResult<TaskDto>> GetAsync(Guid userId, Guid taskId, CancellationToken ct = default);
    Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, Guid clientId, CreateTaskRequest request, CancellationToken ct = default);
    Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid clientId, Guid taskId, UpdateTaskRequest request, CancellationToken ct = default);
    Task<ServiceResult<TaskDto>> ChangeStatusAsync(Guid userId, Guid clientId, Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default);
    Task<ServiceResult> DeleteAsync(Guid userId, Guid clientId, Guid taskId, CancellationToken ct = default);
}
