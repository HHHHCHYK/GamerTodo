using HeyeTodo.Server.Application.Common;
using HeyeTodo.Server.Domain.Entities;
using HeyeTodo.Server.Infrastructure.Persistence;
using HeyeTodo.Shared.Contracts.Tasks;
using HeyeTodo.Shared.Sync;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Server.Application.Projects;

public sealed class ProjectService : IProjectService
{
    private readonly AppDbContext _db;

    public ProjectService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IReadOnlyList<ProjectDto>>> ListAsync(Guid userId, CancellationToken ct = default)
    {
        var projects = await _db.Projects
            .Where(x => x.OwnerId == userId && x.DeletedAt == null)
            .OrderBy(x => x.Name)
            .ToListAsync(ct);

        return ServiceResult<IReadOnlyList<ProjectDto>>.Ok(projects.Select(Map).ToList());
    }

    public async Task<ServiceResult<ProjectDto>> CreateAsync(Guid userId, Guid clientId, CreateProjectRequest request, CancellationToken ct = default)
    {
        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<ProjectDto>.Fail("Project name is required.");
        }

        var now = DateTimeOffset.UtcNow;
        var project = new Project
        {
            OwnerId = userId,
            Name = name,
            Description = NormalizeNullable(request.Description),
            CreatedAt = now,
            UpdatedAt = now,
            UpdatedBy = userId,
            ClientId = clientId,
        };

        StampVersion(project);
        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        return ServiceResult<ProjectDto>.Ok(Map(project));
    }

    public async Task<ServiceResult<ProjectDto>> UpdateAsync(Guid userId, Guid clientId, Guid projectId, UpdateProjectRequest request, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .FirstOrDefaultAsync(x => x.Id == projectId && x.OwnerId == userId && x.DeletedAt == null, ct);
        if (project is null)
        {
            return ServiceResult<ProjectDto>.Fail("Project not found.");
        }

        var name = request.Name.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            return ServiceResult<ProjectDto>.Fail("Project name is required.");
        }

        project.Name = name;
        project.Description = NormalizeNullable(request.Description);
        project.UpdatedAt = DateTimeOffset.UtcNow;
        project.UpdatedBy = userId;
        project.ClientId = clientId;
        StampVersion(project);

        await _db.SaveChangesAsync(ct);
        return ServiceResult<ProjectDto>.Ok(Map(project));
    }

    public async Task<ServiceResult> DeleteAsync(Guid userId, Guid clientId, Guid projectId, CancellationToken ct = default)
    {
        var project = await _db.Projects
            .Include(x => x.Tasks)
            .FirstOrDefaultAsync(x => x.Id == projectId && x.OwnerId == userId && x.DeletedAt == null, ct);
        if (project is null)
        {
            return ServiceResult.Fail("Project not found.");
        }

        var now = DateTimeOffset.UtcNow;
        project.DeletedAt = now;
        project.UpdatedAt = now;
        project.UpdatedBy = userId;
        project.ClientId = clientId;
        StampVersion(project);

        foreach (var task in project.Tasks.Where(x => x.DeletedAt == null))
        {
            task.DeletedAt = now;
            task.UpdatedAt = now;
            task.UpdatedBy = userId;
            task.ClientId = clientId;
            StampVersion(task);
        }

        await _db.SaveChangesAsync(ct);
        return ServiceResult.Ok();
    }

    private long NextServerVersion()
        => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private void StampVersion(SyncableEntity entity)
    {
        entity.ServerVersion = NextServerVersion();
    }

    private static ProjectDto Map(Project project)
        => new(
            project.Id,
            project.OwnerId,
            project.Name,
            project.Description,
            project.CreatedAt,
            new SyncMeta
            {
                ServerVersion = project.ServerVersion,
                UpdatedAt = project.UpdatedAt,
                UpdatedBy = project.UpdatedBy,
                ClientId = project.ClientId,
                DeletedAt = project.DeletedAt,
            });

    private static string? NormalizeNullable(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
