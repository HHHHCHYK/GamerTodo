using HeyeTodo.Server.Domain.Entities;
using HeyeTodo.Shared.Enums;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Server.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<TodoTask> Tasks => Set<TodoTask>();
    public DbSet<TaskDependency> TaskDependencies => Set<TaskDependency>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<AppUser>(e =>
        {
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.Email).HasMaxLength(320).IsRequired();
            e.Property(x => x.DisplayName).HasMaxLength(128).IsRequired();
            e.Property(x => x.PasswordHash).IsRequired();
            e.Property(x => x.Roles).HasConversion<int>();
            e.Property(x => x.ActiveRoleContext).HasConversion<int?>();
            e.Property(x => x.PreferredLanguage).HasMaxLength(16);
        });

        b.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User).WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Project>(e =>
        {
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.ServerVersion);
            e.Property(x => x.Name).HasMaxLength(256).IsRequired();

            e.HasOne(x => x.Owner).WithMany().HasForeignKey(x => x.OwnerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<TodoTask>(e =>
        {
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.ServerVersion);
            e.Property(x => x.Title).HasMaxLength(512).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Priority).HasConversion<int>();
            e.Property(x => x.RoleFieldsJson).HasColumnType("jsonb");

            e.HasOne(x => x.Project).WithMany(p => p.Tasks).HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Assignee).WithMany().HasForeignKey(x => x.AssigneeId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        b.Entity<TaskDependency>(e =>
        {
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => new { x.PredecessorId, x.SuccessorId }).IsUnique();
            e.Property(x => x.Type).HasConversion<int>();

            e.HasOne(x => x.Project).WithMany(p => p.Dependencies).HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Predecessor).WithMany().HasForeignKey(x => x.PredecessorId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Successor).WithMany().HasForeignKey(x => x.SuccessorId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
