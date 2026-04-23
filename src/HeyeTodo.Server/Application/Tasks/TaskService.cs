using System.Text.Json;
using HeyeTodo.Server.Application.Common;
using HeyeTodo.Server.Domain.Entities;
using HeyeTodo.Server.Infrastructure.Persistence;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Server.Application.Tasks;

public sealed class TaskService : ITaskService
{
    private readonly AppDbContext _db;

    public TaskService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IReadOnlyList<TaskDto>>> ListAsync(Guid userId, TaskListQuery query, CancellationToken ct = default)
    {
        var tasks = QueryOwnedTasks(userId);

        if (query.ProjectId is not null)
        {
            tasks = tasks.Where(x => x.ProjectId == query.ProjectId.Value);
        }

        if (query.Status is not null)
        {
            tasks = tasks.Where(x => x.Status == query.Status.Value);
        }

        if (query.Priority is not null)
        {
            tasks = tasks.Where(x => x.Priority == query.Priority.Value);
        }

        if (!query.IncludeCompleted)
        {
            tasks = tasks.Where(x => x.Status != TaskStatus.Done && x.Status != TaskStatus.Cancelled);
        }

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();
            tasks = tasks.Where(x => x.Title.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        }

        tasks = ApplySort(tasks, query.SortBy, query.SortDirection);
        var list = await tasks.ToListAsync(ct);
        return ServiceResult<IReadOnlyList<TaskDto>>.Ok(list.Select(Map).ToList());
    }

    public async Task<ServiceResult<TaskDto>> GetAsync(Guid userId, Guid taskId, CancellationToken ct = default)
    {
        var task = await QueryOwnedTasks(userId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        return task is null
            ? ServiceResult<TaskDto>.Fail("Task not found.")
            : ServiceResult<TaskDto>.Ok(Map(task));
    }

    public async Task<ServiceResult<TaskDto>> CreateAsync(Guid userId, Guid clientId, CreateTaskRequest request, CancellationToken ct = default)
    {
        var projectExists = await _db.Projects.AnyAsync(
            x => x.Id == request.ProjectId && x.OwnerId == userId && x.DeletedAt == null,
            ct);
        if (!projectExists)
        {
            return ServiceResult<TaskDto>.Fail("Project not found.");
        }

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<TaskDto>.Fail("Task title is required.");
        }

        var task = new TodoTask
        {
            ProjectId = request.ProjectId,
            Title = title,
            Description = NormalizeNullable(request.Description),
            Status = request.Status,
            Priority = request.Priority,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EstimatedHours = request.EstimatedHours,
            AssigneeId = request.AssigneeId,
            RoleFieldsJson = NormalizeNullable(request.RoleFieldsJson),
            UpdatedAt = DateTimeOffset.UtcNow,
            UpdatedBy = userId,
            ClientId = clientId,
        };

        StampVersion(task);
        _db.Tasks.Add(task);
        await _db.SaveChangesAsync(ct);
        return ServiceResult<TaskDto>.Ok(Map(task));
    }

    public async Task<ServiceResult<TaskDto>> UpdateAsync(Guid userId, Guid clientId, Guid taskId, UpdateTaskRequest request, CancellationToken ct = default)
    {
        var task = await QueryOwnedTasks(userId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null)
        {
            return ServiceResult<TaskDto>.Fail("Task not found.");
        }

        var projectExists = await _db.Projects.AnyAsync(
            x => x.Id == request.ProjectId && x.OwnerId == userId && x.DeletedAt == null,
            ct);
        if (!projectExists)
        {
            return ServiceResult<TaskDto>.Fail("Project not found.");
        }

        var title = request.Title.Trim();
        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<TaskDto>.Fail("Task title is required.");
        }

        task.ProjectId = request.ProjectId;
        task.Title = title;
        task.Description = NormalizeNullable(request.Description);
        task.Status = request.Status;
        task.Priority = request.Priority;
        task.StartDate = request.StartDate;
        task.EndDate = request.EndDate;
        task.EstimatedHours = request.EstimatedHours;
        task.AssigneeId = request.AssigneeId;
        task.RoleFieldsJson = NormalizeNullable(request.RoleFieldsJson);
        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.UpdatedBy = userId;
        task.ClientId = clientId;
        StampVersion(task);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<TaskDto>.Ok(Map(task));
    }

    public async Task<ServiceResult<TaskDto>> ChangeStatusAsync(Guid userId, Guid clientId, Guid taskId, ChangeTaskStatusRequest request, CancellationToken ct = default)
    {
        var task = await QueryOwnedTasks(userId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null)
        {
            return ServiceResult<TaskDto>.Fail("Task not found.");
        }

        task.Status = request.Status;
        task.UpdatedAt = DateTimeOffset.UtcNow;
        task.UpdatedBy = userId;
        task.ClientId = clientId;
        StampVersion(task);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<TaskDto>.Ok(Map(task));
    }

    public async Task<ServiceResult> DeleteAsync(Guid userId, Guid clientId, Guid taskId, CancellationToken ct = default)
    {
        var task = await QueryOwnedTasks(userId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (task is null)
        {
            return ServiceResult.Fail("Task not found.");
        }

        task.DeletedAt = DateTimeOffset.UtcNow;
        task.UpdatedAt = task.DeletedAt.Value;
        task.UpdatedBy = userId;
        task.ClientId = clientId;
        StampVersion(task);

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private IQueryable<TodoTask> QueryOwnedTasks(Guid userId)
        => _db.Tasks
            .Where(x => x.DeletedAt == null)
            .Join(
                _db.Projects.Where(p => p.OwnerId == userId && p.DeletedAt == null),
                task => task.ProjectId,
                project => project.Id,
                (task, _) => task);

    private IQueryable<TodoTask> ApplySort(IQueryable<TodoTask> query, TaskSortField sortBy, SortDirection direction)
    {
        var descending = direction == SortDirection.Descending;
        return sortBy switch
        {
            TaskSortField.Title => descending ? query.OrderByDescending(x => x.Title) : query.OrderBy(x => x.Title),
            TaskSortField.Priority => descending ? query.OrderByDescending(x => x.Priority).ThenByDescending(x => x.UpdatedAt) : query.OrderBy(x => x.Priority).ThenBy(x => x.UpdatedAt),
            TaskSortField.Status => descending ? query.OrderByDescending(x => x.Status).ThenByDescending(x => x.UpdatedAt) : query.OrderBy(x => x.Status).ThenBy(x => x.UpdatedAt),
            TaskSortField.StartDate => descending ? query.OrderByDescending(x => x.StartDate).ThenByDescending(x => x.UpdatedAt) : query.OrderBy(x => x.StartDate).ThenBy(x => x.UpdatedAt),
            TaskSortField.EndDate => descending ? query.OrderByDescending(x => x.EndDate).ThenByDescending(x => x.UpdatedAt) : query.OrderBy(x => x.EndDate).ThenBy(x => x.UpdatedAt),
            _ => descending ? query.OrderByDescending(x => x.UpdatedAt) : query.OrderBy(x => x.UpdatedAt),
        };
    }

    private long NextServerVersion()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void StampVersion(SyncableEntity entity)
    {
        entity.ServerVersion = NextServerVersion();
    }

    private static TaskDto Map(TodoTask task)
        => new(
            task.Id,
            task.ProjectId,
            task.Title,
            task.Description,
            task.Status,
            task.Priority,
            task.StartDate,
            task.EndDate,
            task.EstimatedHours,
            task.AssigneeId,
            DeserializeRoleFields(task.RoleFieldsJson),
            new SyncMeta
            {
                ServerVersion = task.ServerVersion,
                UpdatedAt = task.UpdatedAt,
                UpdatedBy = task.UpdatedBy,
                ClientId = task.ClientId,
                DeletedAt = task.DeletedAt,
            });

    private static IReadOnlyDictionary<string, object?>? DeserializeRoleFields(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
