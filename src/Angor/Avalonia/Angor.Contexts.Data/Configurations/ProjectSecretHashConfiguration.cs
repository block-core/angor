using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class ProjectSecretHashConfiguration : IEntityTypeConfiguration<ProjectSecretHash>
{
    public void Configure(EntityTypeBuilder<ProjectSecretHash> builder)
    {
        builder.HasKey(e => e.Id);
        
        builder.Property(e => e.ProjectId)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(e => e.SecretHash)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        
        // Foreign key relationship
        builder.HasOne(e => e.Project)
            .WithMany(p => p.SecretHashes)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Unique constraint for ProjectId + SecretHash combination
        builder.HasIndex(e => new { e.ProjectId, e.SecretHash })
            .IsUnique();
            
        // Additional indexes
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.CreatedAt);
    }
}