using Angor.Contexts.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Angor.Contexts.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.HasKey(e => e.ProjectId);
        
        builder.Property(e => e.ProjectId)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(e => e.NostrPubKey)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(e => e.ProjectReceiveAddress)
            .HasMaxLength(100)
            .IsRequired();
            
        builder.Property(e => e.TargetAmount)
            .HasPrecision(18, 8)
            .IsRequired();
            
        builder.Property(e => e.StartDate)
            .IsRequired();
            
        builder.Property(e => e.EndDate)
            .IsRequired();
            
        builder.Property(e => e.PenaltyDays)
            .IsRequired();
            
        builder.Property(e => e.ExpiryDate)
            .IsRequired();
            
        builder.Property(e => e.ProjectSeekerSecretHash)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(e => e.ProjectInfoEventId)
            .HasMaxLength(64)
            .IsRequired();
            
        builder.Property(e => e.CreatedAt)
            .IsRequired();
            
        builder.Property(e => e.UpdatedAt)
            .IsRequired();
        
        // Foreign key relationships
        builder.HasOne(e => e.NostrUser)
            .WithMany()
            .HasForeignKey(e => e.NostrPubKey)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasOne(e => e.NostrEvent)
            .WithMany()
            .HasForeignKey(e => e.ProjectInfoEventId)
            .OnDelete(DeleteBehavior.Restrict);
            
        builder.HasMany(e => e.Stages)
            .WithOne(s => s.Project)
            .HasForeignKey(s => s.ProjectId)
            .OnDelete(DeleteBehavior.Cascade);
        
        // Indexes
        builder.HasIndex(e => e.NostrPubKey);
        builder.HasIndex(e => e.ProjectInfoEventId);
        builder.HasIndex(e => e.ProjectReceiveAddress);
        builder.HasIndex(e => e.StartDate);
        builder.HasIndex(e => e.EndDate);
        builder.HasIndex(e => e.ExpiryDate);
        builder.HasIndex(e => e.CreatedAt);
    }
}