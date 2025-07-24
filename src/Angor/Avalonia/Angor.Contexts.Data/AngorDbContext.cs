using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;

public class AngorDbContext : DbContext
{
    public AngorDbContext(DbContextOptions<AngorDbContext> options) : base(options)
    {
    }
    
    // Add this parameterless constructor for design-time support
    public AngorDbContext() : base()
    {
    }
    
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            // In-memory database for EF migrations/tooling only
            optionsBuilder.UseSqlite("Data Source=:memory:");
        }
    }
    
    // Entity DbSets
    public DbSet<Project> Projects { get; set; }
    public DbSet<ProjectSecretHash> ProjectSecretHashes { get; set; }
    public DbSet<ProjectSecretHash> ProjectStages { get; set; }
    public DbSet<NostrUser> NostrUsers { get; set; } = null!;
    public DbSet<NostrEvent> NostrEvents { get; set; } = null!;
    public DbSet<ProjectKey> ProjectKeys { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AngorDbContext).Assembly);
    }
}