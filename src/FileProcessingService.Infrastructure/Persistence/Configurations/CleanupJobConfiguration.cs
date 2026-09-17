using FileProcessingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileProcessingService.Infrastructure.Persistence.Configurations;

public class CleanupJobConfiguration : IEntityTypeConfiguration<CleanupJob>
{
    public void Configure(EntityTypeBuilder<CleanupJob> builder)
    {
        builder.ToTable("CleanupJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.FilePath)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(j => j.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(j => j.FailureReason)
            .HasMaxLength(2000);

        builder.Property(j => j.CreatedAt)
            .IsRequired();
    }
}
