// Configurations/NostrUserConfiguration.cs
using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class NostrUserConfiguration : IEntityTypeConfiguration<NostrUser>
{
    public void Configure(EntityTypeBuilder<NostrUser> builder)
    {
        builder.HasKey(e => e.PubKey);
        
        builder.Property(e => e.PubKey)
            .HasMaxLength(32)
            .IsRequired();
            
        builder.Property(e => e.DisplayName)
            .HasMaxLength(100);
            
        builder.Property(e => e.About)
            .HasMaxLength(500);
            
        builder.Property(e => e.Picture)
            .HasMaxLength(500);
        
        builder.Property(e => e.Banner)
            .HasMaxLength(500);
            
        builder.Property(e => e.Website)
            .HasMaxLength(200);
            
        builder.Property(e => e.Nip05)
            .HasMaxLength(100);
            
        builder.Property(e => e.IsVerified)
            .HasDefaultValue(false);
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
            
        builder.Property(e => e.UpdatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(e => e.DisplayName);
        builder.HasIndex(e => e.Nip05);
        builder.HasIndex(e => e.CreatedAt);
    }
}