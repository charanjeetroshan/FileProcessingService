using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace FileProcessingService.Infrastructure.Persistence;

/// Enables `dotnet ef migrations`/`database update` to construct this DbContext directly,
/// without needing to run a full Api/Worker host.
public class FileProcessingDbContextFactory : IDesignTimeDbContextFactory<FileProcessingDbContext>
{
    public FileProcessingDbContext CreateDbContext(string[] args)
    {
        var apiConfigPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "FileProcessingService.Api");
        var basePath = Directory.Exists(apiConfigPath) ? apiConfigPath : Directory.GetCurrentDirectory();

        var configuration = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("FileProcessingDatabase")
            ?? throw new InvalidOperationException(
                "Connection string 'FileProcessingDatabase' was not found. Set it via appsettings.json " +
                "or the ConnectionStrings__FileProcessingDatabase environment variable.");

        var optionsBuilder = new DbContextOptionsBuilder<FileProcessingDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new FileProcessingDbContext(optionsBuilder.Options);
    }
}
