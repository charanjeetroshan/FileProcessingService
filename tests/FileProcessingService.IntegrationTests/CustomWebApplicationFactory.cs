using FileProcessingService.Infrastructure.Csv;
using FileProcessingService.Infrastructure.FileStorage;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FileProcessingService.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection connection = new("DataSource=:memory:");
    public string UploadDirectory { get; } = Path.Combine(Path.GetTempPath(), $"file-processing-tests-{Guid.NewGuid()}");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        connection.Open();

        Directory.CreateDirectory(UploadDirectory);

        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:FileProcessingDatabase"] = "DataSource=file-processing-tests;Mode=Memory;Cache=Shared",
                [$"{FileStorageOptions.SectionName}:{nameof(FileStorageOptions.UploadDirectory)}"] = UploadDirectory,
                [$"{CsvOptions.SectionName}:{nameof(CsvOptions.Separator)}"] = ","
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<FileProcessingDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<FileProcessingDbContext>>();
            services.RemoveAll<FileProcessingDbContext>();

            services.AddDbContext<FileProcessingDbContext>(options => options.UseSqlite(connection));

            var serviceProvider = services.BuildServiceProvider();
            using var scope = serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>();
            db.Database.EnsureCreated();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (disposing)
        {
            connection.Dispose();

            if (Directory.Exists(UploadDirectory))
            {
                Directory.Delete(UploadDirectory, recursive: true);
            }
        }
    }
}
