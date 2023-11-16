using Microsoft.EntityFrameworkCore;

namespace Angor.Server;

public class ProjectContext : DbContext
{
    public DbSet<SerializeData> Projects { get; set; }
        
    public DbSet<ProjectKeys> ProjectKeys { get; set; }
    public string DbPath { get; }

    public ProjectContext(string path)
    {
        DbPath = path;
    }

    // The following configures EF to create a Sqlite database file in the
    // special "local" folder for your platform.
    protected override void OnConfiguring(DbContextOptionsBuilder options)
        => options.UseSqlite($"Data Source={DbPath}");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
    }
}