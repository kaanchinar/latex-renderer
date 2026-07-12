using LatexEditor.Core.Entities;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace LatexEditor.Infrastructure.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectFile> ProjectFiles => Set<ProjectFile>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var ns = entityType.ClrType.Namespace;
            if (ns?.StartsWith("Microsoft.AspNetCore.Identity") is true || entityType.ClrType == typeof(ApplicationUser))
            {
                entityType.SetSchema("identity");
            }
        }
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Name).IsRequired().HasMaxLength(200);
            entity.Property(p => p.OwnerId).IsRequired().HasMaxLength(200);
            entity.HasIndex(p => p.OwnerId);
        });

        modelBuilder.Entity<ProjectFile>(entity =>
        {
            entity.HasKey(f => f.Id);
            entity.Property(f => f.Path).IsRequired().HasMaxLength(500);
            entity.Property(f => f.StorageKey).HasMaxLength(1000);
            entity.HasIndex(f => new { f.ProjectId, f.Path }).IsUnique();
        });
    }
}
