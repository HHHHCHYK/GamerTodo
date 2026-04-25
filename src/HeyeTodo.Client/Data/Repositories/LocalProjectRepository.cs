using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Data.Repositories;

public sealed class LocalProjectRepository : IProjectRepository
{
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;

    public LocalProjectRepository(IDbContextFactory<LocalDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<LocalProject>> ListAsync(Guid ownerId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        return await db.Projects
            .Where(x => x.OwnerId == ownerId && x.DeletedAt == null)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);
    }

    public async Task<LocalProject> CreateAsync(Guid ownerId, CreateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var now = DateTimeOffset.UtcNow;
        var entity = new LocalProject
        {
            OwnerId = ownerId,
            Name = request.Name.Trim(),
            Description = NormalizeNullable(request.Description),
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = ownerId,
            ClientId = clientId,
            IsDirty = true,
        };

        db.Projects.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<LocalProject?> UpdateAsync(Guid ownerId, UpdateProjectRequest request, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Projects
            .FirstOrDefaultAsync(x => x.Id == request.Id && x.OwnerId == ownerId && x.DeletedAt == null, ct);
        if (entity is null)
        {
            return null;
        }

        entity.Name = request.Name.Trim();
        entity.Description = NormalizeNullable(request.Description);
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        entity.UpdatedBy = ownerId;
        entity.ClientId = clientId;
        entity.IsDirty = true;

        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<bool> DeleteAsync(Guid ownerId, Guid projectId, Guid clientId, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId && x.OwnerId == ownerId && x.DeletedAt == null, ct);
        if (entity is null)
        {
            return false;
        }

        var now = DateTimeOffset.UtcNow;
        entity.DeletedAt = now;
        entity.UpdatedAt = now;
        entity.UpdatedBy = ownerId;
        entity.ClientId = clientId;
        entity.IsDirty = true;

        var tasks = await db.Tasks
            .Where(x => x.ProjectId == entity.Id && x.DeletedAt == null)
            .ToListAsync(ct);
        foreach (var task in tasks)
        {
            task.DeletedAt = now;
            task.UpdatedAt = now;
            task.UpdatedBy = ownerId;
            task.ClientId = clientId;
            task.IsDirty = true;
        }

        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<LocalProject?> UpdateSyncMetadataAsync(Guid ownerId, Guid projectId, SyncMeta sync, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var entity = await db.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId && x.OwnerId == ownerId, ct);
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

    public async Task UpsertFromRemoteAsync(Guid ownerId, IReadOnlyList<ProjectDto> projects, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        foreach (var dto in projects)
        {
            var entity = await db.Projects.FirstOrDefaultAsync(x => x.Id == dto.Id && x.OwnerId == ownerId, ct);
            if (entity is not null && entity.IsDirty && entity.UpdatedAt > dto.Sync.UpdatedAt)
            {
                continue;
            }

            if (entity is not null && entity.ServerVersion >= dto.Sync.ServerVersion && dto.Sync.ServerVersion != 0)
            {
                continue;
            }

            if (entity is null)
            {
                entity = new LocalProject
                {
                    Id = dto.Id,
                    OwnerId = ownerId,
                    CreatedAt = dto.CreatedAt,
                };
                db.Projects.Add(entity);
            }

            entity.Name = dto.Name;
            entity.Description = dto.Description;
            entity.ServerVersion = dto.Sync.ServerVersion;
            entity.UpdatedAt = dto.Sync.UpdatedAt;
            entity.UpdatedBy = dto.Sync.UpdatedBy;
            entity.ClientId = dto.Sync.ClientId;
            entity.DeletedAt = dto.Sync.DeletedAt;
            entity.IsDirty = false;
        }

        await db.SaveChangesAsync(ct);
    }

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
