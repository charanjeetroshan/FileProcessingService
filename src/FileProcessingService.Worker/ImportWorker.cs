using System.Collections.Concurrent;
using FileProcessingService.Application.Imports;

namespace FileProcessingService.Worker;

public class ImportWorker(IServiceScopeFactory scopeFactory, ILogger<ImportWorker> logger) : BackgroundService
{
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(10);
    private const int MaxConcurrentJobs = 4;

    private readonly SemaphoreSlim concurrencyLimiter = new(MaxConcurrentJobs, MaxConcurrentJobs);
    private readonly ConcurrentDictionary<Guid, Task> runningJobs = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("ImportWorker started at: {Time} with max concurrency {MaxConcurrentJobs}", DateTimeOffset.UtcNow, MaxConcurrentJobs);

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await ClaimAndStartAvailableJobsAsync(stoppingToken);
                await Task.Delay(PollingInterval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Expected during shutdown.
        }
        finally
        {
            logger.LogInformation("ImportWorker waiting for {RunningJobCount} in-flight job(s) to complete before stopping", runningJobs.Count);
            await Task.WhenAll(runningJobs.Values);
            logger.LogInformation("ImportWorker stopped at: {Time}", DateTimeOffset.UtcNow);
        }
    }

    private async Task ClaimAndStartAvailableJobsAsync(CancellationToken stoppingToken)
    {
        // Keep claiming pending jobs while concurrency slots are free; once slots are full
        // or no pending jobs remain, fall back to the polling delay.
        while (!stoppingToken.IsCancellationRequested && await concurrencyLimiter.WaitAsync(0, stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();

            var job = await jobRepository.ClaimNextPendingJobAsync(stoppingToken);
            if (job is null)
            {
                concurrencyLimiter.Release();
                return;
            }

            logger.LogInformation("Claimed pending import job {ImportJobId} ({OriginalFileName})", job.Id, job.OriginalFileName);

            var jobId = job.Id;
            var runTask = RunJobAsync(jobId, stoppingToken);
            runningJobs[jobId] = runTask;
        }
    }

    private async Task RunJobAsync(Guid jobId, CancellationToken stoppingToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var jobRepository = scope.ServiceProvider.GetRequiredService<IImportJobRepository>();
            var processor = scope.ServiceProvider.GetRequiredService<IImportJobProcessor>();

            var job = await jobRepository.GetByIdAsync(jobId, stoppingToken);
            if (job is null)
            {
                logger.LogWarning("Claimed import job {ImportJobId} could not be reloaded for processing", jobId);
                return;
            }

            await processor.ProcessJob(job, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled error while processing import job {ImportJobId}", jobId);
        }
        finally
        {
            runningJobs.TryRemove(jobId, out _);
            concurrencyLimiter.Release();
        }
    }
}
