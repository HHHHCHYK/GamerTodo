using HeyeTodo.Client.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace HeyeTodo.Client.Data;

public sealed class LocalDbContext : DbContext
{
    public LocalDbContext(DbContextOptions<LocalDbContext> options) : base(options) { }

    public DbSet<LocalProject> Projects => Set<LocalProject>();
    public DbSet<LocalTask> Tasks => Set<LocalTask>();
    public DbSet<LocalDependency> Dependencies => Set<LocalDependency>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<LocalProject>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.OwnerId);
            e.Property(x => x.Name).IsRequired();
        });

        b.Entity<LocalTask>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProjectId);
            e.Property(x => x.Title).IsRequired();
            e.Property(x => x.Status).HasConversion<int>();
            e.Property(x => x.Priority).HasConversion<int>();
        });

        b.Entity<LocalDependency>(e =>
        {
            e.HasKey(x => x.Id);
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => new { x.PredecessorId, x.SuccessorId }).IsUnique();
            e.Property(x => x.Type).HasConversion<int>();
        });
    }
}
