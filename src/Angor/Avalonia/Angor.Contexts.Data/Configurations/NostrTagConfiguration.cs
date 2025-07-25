// Configurations/NostrTagConfiguration.cs
using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class NostrTagConfiguration : IEntityTypeConfiguration<NostrTag>
{
    public void Configure(EntityTypeBuilder<NostrTag> builder)
    {
        builder.HasKey(t => t.Name);

        builder.Property(t => t.Name)
            .HasMaxLength(32)
            .IsRequired();

        builder.Property(t => t.EventId)
            .HasMaxLength(32)
            .IsRequired();

        builder.HasIndex(t => t.EventId);

        builder.Property(t => t.Content)
            .HasConversion(
                v => string.Join(',', v), // Convert List<string> to a comma-separated string
                v => v.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() // Convert back to List<string>
            )
            .IsRequired();

        builder.HasOne(t => t.Event)
            .WithMany(e => e.Tags)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}