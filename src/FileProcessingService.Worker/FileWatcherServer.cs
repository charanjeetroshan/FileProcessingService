using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.FileStorage;
using Microsoft.Extensions.Options;

namespace FileProcessingService.Worker;

public class FileWatcherService(
    IOptions<FileStorageOptions> options,
    ILogger<FileWatcherService> logger,
    IServiceScopeFactory scopeFactory
) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(15);
    private readonly string uploadDirectory = options.Value.UploadDirectoryPath;
    private readonly string exportDirectory = options.Value.ExportDirectoryPath;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation(
            "FileWatcherService started. Watching folders: {UploadDirectory} and {ExportDirectory}", uploadDirectory, exportDirectory);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var cleanupJobRepository = scope.ServiceProvider.GetRequiredService<ICleanupJobRepository>();
                var importJobRepository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
                var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

                var existingCleanupFilePaths = await cleanupJobRepository.GetExistingCleanupFilePathsAsync(stoppingToken);

                string[] uploadedFilePaths = fileStorageService.GetFilesAt(uploadDirectory);
                await HandleUploadedFiles(importJobRepository, cleanupJobRepository, uploadedFilePaths, existingCleanupFilePaths, stoppingToken);

                string[] exportedFilePaths = fileStorageService.GetFilesAt(exportDirectory);
                await HandleExportedFiles(cleanupJobRepository, exportedFilePaths, existingCleanupFilePaths, stoppingToken);

                await HandleAnonymousFiles(importJobRepository, cleanupJobRepository, uploadedFilePaths, existingCleanupFilePaths, stoppingToken);

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

    private async Task HandleUploadedFiles(
        IImportJobRepository importJobRepository,
        ICleanupJobRepository cleanupJobRepository,
        string[] uploadedFilePaths,
        HashSet<string> existingCleanupFilePaths,
        CancellationToken cancellationToken)
    {
        string[] fileNames = [.. uploadedFilePaths.Select(f => Path.GetFileName(f))];
        var completedImportJobs = await importJobRepository.GetCompletedJobsByFileNamesAsync(fileNames, cancellationToken);

        var candidatePaths = completedImportJobs
            .Select(job => Path.Combine(uploadDirectory, job.StoredFileName))
            .Where(filePath => !existingCleanupFilePaths.Contains(filePath));

        await CreateCleanupJobsAsync(cleanupJobRepository, candidatePaths, "uploaded", uploadDirectory, cancellationToken);
    }

    private async Task HandleExportedFiles(
        ICleanupJobRepository cleanupJobRepository,
        string[] exportedFilePaths,
        HashSet<string> existingCleanupFilePaths,
        CancellationToken cancellationToken)
    {
        var expiryThreshold = DateTime.UtcNow.AddDays(-options.Value.FileKeepDurationInDays);

        var candidatePaths = exportedFilePaths.Where(filePath =>
            !existingCleanupFilePaths.Contains(filePath) &&
            new FileInfo(filePath).CreationTimeUtc < expiryThreshold);

        await CreateCleanupJobsAsync(cleanupJobRepository, candidatePaths, "exported", exportDirectory, cancellationToken);
    }

    private async Task HandleAnonymousFiles(
        IImportJobRepository importJobRepository,
        ICleanupJobRepository cleanupJobRepository,
        string[] filePaths,
        HashSet<string> existingCleanupFilePaths,
        CancellationToken cancellationToken)
    {
        var trackedFileNames = await importJobRepository.GetAllTrackedFileNamesAsync(cancellationToken);

        var candidatePaths = filePaths.Where(filePath =>
            !existingCleanupFilePaths.Contains(filePath) &&
            !trackedFileNames.Contains(Path.GetFileName(filePath)));

        await CreateCleanupJobsAsync(cleanupJobRepository, candidatePaths, "anonymous", uploadDirectory, cancellationToken);
    }

    private async Task CreateCleanupJobsAsync(
        ICleanupJobRepository cleanupJobRepository,
        IEnumerable<string> candidatePaths,
        string fileCategory,
        string scannedDirectory,
        CancellationToken cancellationToken)
    {
        var cleanupJobs = candidatePaths
            .Select(filePath => new CleanupJob { FilePath = filePath, CreatedAt = DateTimeOffset.UtcNow })
            .ToList();

        if (cleanupJobs.Count == 0)
        {
            logger.LogDebug("Scan at {Directory} for {FileCategory} files completed. No jobs were created.", scannedDirectory, fileCategory);
            return;
        }

        foreach (var cleanupJob in cleanupJobs)
        {
            logger.LogInformation("Creating cleanup job for {FileCategory} file: {FilePath}", fileCategory, cleanupJob.FilePath);
        }

        await cleanupJobRepository.AddJobsAsync(cleanupJobs, cancellationToken);
        logger.LogDebug(
            "Scan at {Directory} for {FileCategory} files completed. Total jobs created: {Count}", scannedDirectory, fileCategory, cleanupJobs.Count);
    }
}
