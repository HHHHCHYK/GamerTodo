using HeyeTodo.Client.Data.Entities;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Data.Repositories;

public sealed class LocalDependencyRepository : IDependencyRepository
{
    private readonly IDbContextFactory<LocalDbContext> _dbFactory;

    public LocalDependencyRepository(IDbContextFactory<LocalDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<LocalDependency>> ListAsync(Guid ownerId, Guid? projectId = null, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var ownedProjectIds = db.Projects.Where(x => x.OwnerId == ownerId).Select(x => x.Id);
        var query = db.Dependencies.Where(x => x.DeletedAt == null && ownedProjectIds.Contains(x.ProjectId));
        if (projectId is not null)
        {
            query = query.Where(x => x.ProjectId == projectId.Value);
        }

        return await query.ToListAsync(ct);
    }

    public async Task UpsertFromRemoteAsync(Guid ownerId, IReadOnlyList<TaskDependencyDto> dependencies, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var ownedProjectIds = await db.Projects.Where(x => x.OwnerId == ownerId).Select(x => x.Id).ToListAsync(ct);

        foreach (var dto in dependencies)
        {
            if (!ownedProjectIds.Contains(dto.ProjectId))
            {
                continue;
            }

            var entity = await db.Dependencies.FirstOrDefaultAsync(x => x.Id == dto.Id, ct);
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
                entity = new LocalDependency { Id = dto.Id };
                db.Dependencies.Add(entity);
            }

            entity.ProjectId = dto.ProjectId;
            entity.PredecessorId = dto.PredecessorId;
            entity.SuccessorId = dto.SuccessorId;
            entity.Type = dto.Type;
            entity.ServerVersion = dto.Sync.ServerVersion;
            entity.UpdatedAt = dto.Sync.UpdatedAt;
            entity.UpdatedBy = dto.Sync.UpdatedBy;
            entity.ClientId = dto.Sync.ClientId;
            entity.DeletedAt = dto.Sync.DeletedAt;
            entity.IsDirty = false;
        }

        await db.SaveChangesAsync(ct);
    }

    public async Task<LocalDependency?> UpdateSyncMetadataAsync(Guid ownerId, Guid dependencyId, SyncMeta sync, CancellationToken ct = default)
    {
        await using var db = await _dbFactory.CreateDbContextAsync(ct);
        var ownedProjectIds = db.Projects.Where(x => x.OwnerId == ownerId).Select(x => x.Id);
        var entity = await db.Dependencies
            .Where(x => ownedProjectIds.Contains(x.ProjectId))
            .FirstOrDefaultAsync(x => x.Id == dependencyId, ct);
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
}
