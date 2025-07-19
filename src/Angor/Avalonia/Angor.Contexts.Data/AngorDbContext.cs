using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Angor.Contexts.Data;

public class AngorDbContext : DbContext
{
    public AngorDbContext(DbContextOptions<AngorDbContext> options)
        : base(options)
    {
    }

    public DbSet<NostrUser> NostrUsers { get; set; } = null!;
    public DbSet<NostrEvent> NostrEvents { get; set; } = null!;

    //public DbSet<BlockchainTransaction> BlockchainTransactions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Apply all configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AngorDbContext).Assembly);
    }
}