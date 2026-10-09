using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Application.Configuration;
using FileProcessingService.Domain.Entities;
using Microsoft.Extensions.Options;

namespace FileProcessingService.Worker;

public class FileWatcherService(
    IOptions<FileStorageOptions> options,
    ILogger<FileWatcherService> logger,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private readonly string exportDirectory = options.Value.ExportDirectoryPath;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("FileWatcherService started. Watching folder: {ExportDirectory}", exportDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cleanupJobRepository = scope.ServiceProvider.GetRequiredService<ICleanupJobRepository>();
                var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                var existingCleanupFilePaths = await cleanupJobRepository.GetExistingCleanupFilePathsAsync(stoppingToken);

                string[] exportedFilePaths = fileStorageService.GetFilesAt(exportDirectory);
                await HandleExportedFiles(cleanupJobRepository, exportedFilePaths, existingCleanupFilePaths, stoppingToken);

                await cleanupJobRepository.SaveChangesAsync(stoppingToken);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Unhandled error in file watcher service. Reason: {Reason}", ex.Message);
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("FileWatcherService stopped at: {Time}", DateTimeOffset.UtcNow);
    }

    private async Task HandleExportedFiles(
        ICleanupJobRepository cleanupJobRepository,
        string[] exportedFilePaths,
        HashSet<string> existingCleanupFilePaths,
        CancellationToken cancellationToken)
    {
        logger.LogTrace("Start scanning {Directory} for exported files", exportDirectory);

        List<CleanupJob> cleanupJobs = [];

        foreach (string filePath in exportedFilePaths)
        {
            var fileInfo = new FileInfo(filePath);
            DateTime expiryThreshold = fileInfo.CreationTimeUtc.AddDays(options.Value.FileKeepDurationInDays);

            if (!existingCleanupFilePaths.Contains(filePath, StringComparer.OrdinalIgnoreCase) &&
                DateTime.UtcNow > expiryThreshold)
            {
                var job = new CleanupJob
                {
                    FilePath = filePath,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                cleanupJobs.Add(job);

                logger.LogInformation("Creating cleanup job for exported file: {FilePath}", filePath);
            }
        }

        if (cleanupJobs.Count == 0)
        {
            logger.LogTrace("Scan at {Directory} for exported files completed. No jobs were created.", exportDirectory);
            return;
        }

        await cleanupJobRepository.AddJobsAsync(cleanupJobs, cancellationToken);
        logger.LogTrace(
            "Scan at {Directory} for exported files completed. Total jobs created: {Count}", exportDirectory, cleanupJobs.Count);
    }
}
