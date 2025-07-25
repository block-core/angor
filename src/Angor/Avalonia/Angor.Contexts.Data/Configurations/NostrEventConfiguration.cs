// Configurations/NostrEventConfiguration.cs
using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class NostrEventConfiguration : IEntityTypeConfiguration<NostrEvent>
{
    public void Configure(EntityTypeBuilder<NostrEvent> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.Id)
            .HasMaxLength(32)
            .IsRequired();
            
        builder.Property(e => e.PubKey)
            .HasMaxLength(32)
            .IsRequired();
            
        builder.Property(e => e.Kind)
            .IsRequired();
            
        builder.Property(e => e.Content)
            .HasMaxLength(4000)
            .IsRequired();
            
        builder.Property(e => e.Signature)
            .HasMaxLength(128)
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        
        builder.HasMany(e => e.Tags)
            .WithOne(t => t.Event)
            .HasForeignKey(t => t.EventId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        builder.HasIndex(e => e.PubKey);
        builder.HasIndex(e => e.CreatedAt);
        builder.HasIndex(e => e.Kind);
        
        // Foreign key relationship
        builder.HasOne(e => e.User)
            .WithMany(u => u.Events)
            .HasForeignKey(e => e.PubKey)
            .HasPrincipalKey(u => u.PubKey)
            .OnDelete(DeleteBehavior.Cascade);
    }
}