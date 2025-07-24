using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class ProjectKeyConfiguration : IEntityTypeConfiguration<ProjectKey>
{
    public void Configure(EntityTypeBuilder<ProjectKey> builder)
    {
        builder.HasKey(pk => pk.Id);
        
        builder.Property(pk => pk.WalletId)
            .IsRequired();
            
        builder.Property(pk => pk.ProjectId)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(pk => pk.NostrPubKey)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(pk => pk.CreatedAt)
            .IsRequired();
            
        builder.Property(pk => pk.UpdatedAt)
            .IsRequired();
        
        // Indexes
        builder.HasIndex(pk => pk.WalletId);
        builder.HasIndex(pk => new { pk.WalletId, pk.ProjectId }).IsUnique();
        builder.HasIndex(pk => pk.NostrPubKey);
    }
}