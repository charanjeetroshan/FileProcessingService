using FileProcessingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileProcessingService.Infrastructure.Persistence.Configurations;

public class ImportJobConfiguration : IEntityTypeConfiguration<ImportJob>
{
    public void Configure(EntityTypeBuilder<ImportJob> builder)
    {
        builder.ToTable("ImportJobs");

        builder.HasKey(j => j.Id);

        builder.Property(j => j.OriginalFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(j => j.StoredFileName)
            .HasMaxLength(260)
            .IsRequired();

        builder.Property(j => j.FileHash)
            .HasMaxLength(128);

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
