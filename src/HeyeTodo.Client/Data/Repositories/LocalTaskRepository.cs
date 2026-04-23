using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Enums;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;
using TaskStatus = HeyeTodo.Shared.Enums.TaskStatus;

namespace HeyeTodo.Client.Data.Repositories;

public sealed class LocalTaskRepository : ITaskRepository
{
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;

    public LocalTaskRepository(IDbContextFactory<LocalDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<LocalTask>> ListAsync(Guid ownerId, TaskListQuery query, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var accessibleProjectIds = db.Projects
            .Where(x => x.OwnerId == ownerId && x.DeletedAt == null)
            .Select(x => x.Id);

        var tasks = db.Tasks
            .Where(x => x.DeletedAt == null && accessibleProjectIds.Contains(x.ProjectId));

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
        return await tasks.ToListAsync(ct);
    }

    public async Task<LocalTask?> GetAsync(Guid ownerId, Guid taskId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await QueryOwnedTasks(db, ownerId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
    }

    public async Task<LocalTask> CreateAsync(Guid ownerId, CreateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var projectExists = await db.Projects.AnyAsync(
            x => x.Id == request.ProjectId && x.OwnerId == ownerId && x.DeletedAt == null,
            ct);
        if (!projectExists)
        {
            throw new InvalidOperationException("Project not found.");
        }

        var now = DateTimeOffset.UtcNow;
        var entity = new LocalTask
        {
            ProjectId = request.ProjectId,
            Title = request.Title.Trim(),
            Description = NormalizeNullable(request.Description),
            Status = request.Status,
            Priority = request.Priority,
            StartDate = request.StartDate,
            EndDate = request.EndDate,
            EstimatedHours = request.EstimatedHours,
            AssigneeId = request.AssigneeId,
            RoleFieldsJson = NormalizeNullable(request.RoleFieldsJson),
            UpdatedAt = now,
            UpdatedBy = ownerId,
            ClientId = clientId,
            IsDirty = true,
        };

        db.Tasks.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<LocalTask?> UpdateAsync(Guid ownerId, UpdateTaskRequest request, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await QueryOwnedTasks(db, ownerId)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (entity is null)
        {
            return null;
        }

        var projectExists = await db.Projects.AnyAsync(
            x => x.Id == request.ProjectId && x.OwnerId == ownerId && x.DeletedAt == null,
            ct);
        if (!projectExists)
        {
            return null;
        }

        entity.ProjectId = request.ProjectId;
        entity.Title = request.Title.Trim();
        entity.Description = NormalizeNullable(request.Description);
        entity.Status = request.Status;
        entity.Priority = request.Priority;
        entity.StartDate = request.StartDate;
        entity.EndDate = request.EndDate;
        entity.EstimatedHours = request.EstimatedHours;
        entity.AssigneeId = request.AssigneeId;
        entity.RoleFieldsJson = NormalizeNullable(request.RoleFieldsJson);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = ownerId;
        entity.ClientId = clientId;
        entity.IsDirty = true;

        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<LocalTask?> ChangeStatusAsync(Guid ownerId, ChangeTaskStatusRequest request, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await QueryOwnedTasks(db, ownerId)
            .FirstOrDefaultAsync(x => x.Id == request.Id, ct);
        if (entity is null)
        {
            return null;
        }

        entity.Status = request.Status;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = ownerId;
        entity.ClientId = clientId;
        entity.IsDirty = true;

        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid taskId, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await QueryOwnedTasks(db, ownerId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (entity is null)
        {
            return false;
        }

        entity.DeletedAt = DateTimeOffset.UtcNow;
        entity.UpdatedAt = entity.DeletedAt.Value;
        entity.UpdatedBy = ownerId;
        entity.ClientId = clientId;
        entity.IsDirty = true;

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LocalTask?> UpdateSyncMetadataAsync(Guid ownerId, Guid taskId, SyncMeta sync, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await QueryOwnedTasks(db, ownerId)
            .FirstOrDefaultAsync(x => x.Id == taskId, ct);
        if (entity is null)
        {
            return null;
        }

        entity.ServerVersion = sync.ServerVersion;
        entity.UpdatedAt = sync.UpdatedAt;
        entity.UpdatedBy = sync.UpdatedBy;
        entity.ClientId = sync.ClientId;
        entity.DeletedAt = sync.DeletedAt;
        entity.IsDirty = false;
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task UpsertFromRemoteAsync(Guid ownerId, IReadOnlyList<TaskDto> tasks, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        foreach (var dto in tasks)
        {
            var projectExists = await db.Projects.AnyAsync(x => x.Id == dto.ProjectId && x.OwnerId == ownerId && x.DeletedAt == null, ct);
            if (!projectExists)
            {
                continue;
            }

            var entity = await db.Tasks.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
            if (entity is null)
            {
                entity = new LocalTask
                {
                    Id = dto.Id,
                };
                db.Tasks.Add(entity);
            }

            entity.ProjectId = dto.ProjectId;
            entity.Title = dto.Title;
            entity.Description = dto.Description;
            entity.Status = dto.Status;
            entity.Priority = dto.Priority;
            entity.StartDate = dto.StartDate;
            entity.EndDate = dto.EndDate;
            entity.EstimatedHours = dto.EstimatedHours;
            entity.AssigneeId = dto.AssigneeId;
            entity.RoleFieldsJson = dto.RoleFields is null ? null : JsonSerializer.Serialize(dto.RoleFields);
            entity.ServerVersion = dto.Sync.ServerVersion;
            entity.UpdatedAt = dto.Sync.UpdatedAt;
            entity.UpdatedBy = dto.Sync.UpdatedBy;
            entity.ClientId = dto.Sync.ClientId;
            entity.DeletedAt = dto.Sync.DeletedAt;
            entity.IsDirty = false;
        }

        await db.SaveChangesAsync(ct);
    }

    private static IQueryable<LocalTask> QueryOwnedTasks(LocalDbContext db, Guid ownerId)
        => db.Tasks.Where(t => t.DeletedAt == null)
            .Join(
                db.Projects.Where(p => p.OwnerId == ownerId && p.DeletedAt == null),
                task => task.ProjectId,
                project => project.Id,
                (task, _) => task);

    private static IQueryable<LocalTask> ApplySort(IQueryable<LocalTask> query, TaskSortField sortBy, SortDirection direction)
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

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
