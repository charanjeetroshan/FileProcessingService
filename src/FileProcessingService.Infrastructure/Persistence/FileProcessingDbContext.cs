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

        base.OnModelCreating(modelBuilder);
    }
}
