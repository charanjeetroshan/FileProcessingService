using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Domain.Enums;

namespace FileProcessingService.Worker;

public class CleanupWorker(IServiceScopeFactory scopeFactory, ILogger<CleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private const int BatchSize = 10;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("CleanupWorker started at: {Time}", DateTimeOffset.UtcNow);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCleanup(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected during shutdown.
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Unhandled error while running cleanup cycle.");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }

        logger.LogInformation("CleanupWorker stopped at: {Time}", DateTimeOffset.UtcNow);
    }

    private async Task RunCleanup(CancellationToken stoppingToken)
    {
        using var scope = scopeFactory.CreateScope();
        var cleanupJobRepository = scope.ServiceProvider.GetRequiredService<ICleanupJobRepository>();
        var fileStorageService = scope.ServiceProvider.GetRequiredService<IFileStorageService>();

        var cleanupJobs = await cleanupJobRepository.ClaimByCountAsync(count: BatchSize, stoppingToken);

        if (cleanupJobs.Length == 0)
        {
            return;
        }

        logger.LogInformation("Found {Count} cleanup jobs to process.", cleanupJobs.Length);

        foreach (var job in cleanupJobs)
        {
            logger.LogInformation("Processing cleanup job with ID: {JobId}", job.Id);

            try
            {
                logger.LogInformation("Cleaning up file at path: {FilePath}", job.FilePath);

                fileStorageService.DeleteFile(job.FilePath);
                job.Status = CleanupStatus.Done;
                job.CompletedAt = DateTimeOffset.UtcNow;

                logger.LogInformation("Cleanup job with ID: {JobId} completed successfully.", job.Id);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "Cleanup job with ID: {JobId} failed. Reason: {Reason}", job.Id, ex.Message);
                job.Status = CleanupStatus.Failed;
                job.FailureReason = ex.Message;
            }
        }

        await cleanupJobRepository.SaveChangesAsync(stoppingToken);
    }
}
