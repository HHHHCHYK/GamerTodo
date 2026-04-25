using HeyeTodo.Client.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Data;

public sealed class LocalDbContext : DbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }

    public DbSet<LocalProject> Projects => Set<LocalProject>();
    public DbSet<LocalTask> Tasks => Set<LocalTask>();
    public DbSet<LocalDependency> Dependencies => Set<LocalDependency>();
    public DbSet<LocalOutboxItem> Outbox => Set<LocalOutboxItem>();
    public DbSet<LocalInboxItem> Inbox => Set<LocalInboxItem>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<LocalProject>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OwnerId);
            e.HasIndex(x => x.ServerVersion);
            e.Property(x => x.Name).IsRequired();
        });

        b.Entity<LocalTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.ServerVersion);
            e.Property(x => x.Title).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Priority).HasConversion<int>();
        });

        b.Entity<LocalDependency>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.ServerVersion);
            e.HasIndex(x => new { x.PredecessorId, x.SuccessorId }).IsUnique();
            e.Property(x => x.Type).HasConversion<int>();
        });

        b.Entity<LocalOutboxItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OwnerId, x.AcknowledgedAt });
            e.HasIndex(x => new { x.OwnerId, x.EntityType, x.EntityId });
            e.Property(x => x.EntityType).HasConversion<int>();
            e.Property(x => x.Operation).HasConversion<int>();
            e.Property(x => x.PayloadJson).IsRequired();
        });

        b.Entity<LocalInboxItem>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.OwnerId, x.ServerVersion });
            e.HasIndex(x => new { x.OwnerId, x.EntityType, x.EntityId, x.ServerVersion }).IsUnique();
            e.Property(x => x.EntityType).HasConversion<int>();
            e.Property(x => x.Operation).HasConversion<int>();
            e.Property(x => x.PayloadJson).IsRequired();
        });
    }
}
