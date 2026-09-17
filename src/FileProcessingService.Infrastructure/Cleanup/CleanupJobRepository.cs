using FileProcessingService.Application.Cleanup;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileProcessingService.Infrastructure.Cleanup;

public class CleanupJobRepository(FileProcessingDbContext db) : ICleanupJobRepository
{
    public async Task AddJobsAsync(IEnumerable<CleanupJob> jobs, CancellationToken cancellationToken = default)
    {
        await db.CleanupJobs.AddRangeAsync(jobs, cancellationToken);
    }

    public async Task<CleanupJob[]> ClaimByCountAsync(int count = 10, CancellationToken cancellationToken = default)
    {
        var jobs = await db.CleanupJobs
            .Where(job => job.Status == CleanupStatus.Pending)
            .OrderBy(job => job.CreatedAt)
            .Take(count)
            .ToArrayAsync(cancellationToken);

        foreach (var job in jobs)
        {
            job.Status = CleanupStatus.CleaningUp;
        }

        await db.SaveChangesAsync(cancellationToken);

        return jobs;
    }

    public async Task<HashSet<string>> GetExistingCleanupFilePathsAsync(CancellationToken cancellationToken = default)
    {
        var filePaths = await db.CleanupJobs
            .Where(job => job.Status != CleanupStatus.Done)
            .Select(job => job.FilePath)
            .ToArrayAsync(cancellationToken);

        return new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await db.SaveChangesAsync(cancellationToken);
    }
}
