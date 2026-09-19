using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Persistence.Configurations;
using Microsoft.EntityFrameworkCore;

namespace FileProcessingService.Infrastructure.Persistence;

public class FileProcessingDbContext(DbContextOptions<FileProcessingDbContext> options) : DbContext(options)
{
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<ImportJob> ImportJobs => Set<ImportJob>();
    public DbSet<ImportError> ImportErrors => Set<ImportError>();
    public DbSet<CleanupJob> CleanupJobs => Set<CleanupJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new CustomerConfiguration());
        modelBuilder.ApplyConfiguration(new ImportJobConfiguration());
        modelBuilder.ApplyConfiguration(new ImportErrorConfiguration());
        modelBuilder.ApplyConfiguration(new CleanupJobConfiguration());

        // SQLite has no native DateTimeOffset type and cannot translate ORDER BY/comparisons
        // against it. This only affects the SQLite provider (used by in-memory tests) and
        // leaves the SQL Server (production) schema/configuration untouched.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.Sqlite")
        {
            var converter = new DateTimeOffsetTicksConverter();

            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                foreach (var property in entityType.GetProperties())
                {
                    if (property.ClrType == typeof(DateTimeOffset))
                    {
                        property.SetValueConverter(converter);
                    }
                }
            }
        }

        base.OnModelCreating(modelBuilder);
    }
}
