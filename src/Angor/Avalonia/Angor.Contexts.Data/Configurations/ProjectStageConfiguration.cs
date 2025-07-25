using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class ProjectStageConfiguration : IEntityTypeConfiguration<ProjectStage>
{
    public void Configure(EntityTypeBuilder<ProjectStage> builder)
    {
        // Composite primary key
        builder.HasKey(e => new { e.ProjectId, e.StageIndex });
        
        builder.Property(e => e.ProjectId)
            .HasMaxLength(32)
            .IsRequired();
            
        builder.Property(e => e.StageIndex)
            .IsRequired();
            
        builder.Property(e => e.AmountToRelease)
            .HasPrecision(18, 8)
            .IsRequired();
            
        builder.Property(e => e.ReleaseDate)
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
        
        // Foreign key relationship
        builder.HasOne(e => e.Project)
            .WithMany(p => p.Stages)
            .HasForeignKey(e => e.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        builder.HasIndex(e => e.ProjectId);
        builder.HasIndex(e => e.ReleaseDate);
        builder.HasIndex(e => e.CreatedAt);
    }
}