using FileProcessingService.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace FileProcessingService.Infrastructure.Persistence.Configurations;

public class ImportErrorConfiguration : IEntityTypeConfiguration<ImportError>
{
    public void Configure(EntityTypeBuilder<ImportError> builder)
    {
        builder.ToTable("ImportErrors");

        builder.HasKey(e => e.Id);

        builder.Property(e => e.Field)
            .HasMaxLength(200);

        builder.Property(e => e.ErrorCode)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(e => e.ErrorMessage)
            .HasMaxLength(1000)
            .IsRequired();

        builder.Property(e => e.RawValue)
            .HasMaxLength(1000);

        builder.HasIndex(e => e.ImportJobId);
    }
}
