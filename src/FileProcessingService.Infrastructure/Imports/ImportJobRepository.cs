using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileProcessingService.Infrastructure.Imports;

public class ImportJobRepository(FileProcessingDbContext dbContext) : IImportJobRepository
{
    public async Task AddAsync(ImportJob job, CancellationToken cancellationToken = default)
    {
        await dbContext.ImportJobs.AddAsync(job, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => dbContext.ImportJobs.FirstOrDefaultAsync(job => job.Id == id, cancellationToken);

    public Task<ImportJob?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
        => dbContext.ImportJobs
            .AsNoTracking()
            .Where(job => job.FileHash == fileHash
                && job.Status != ImportStatus.Failed
                && job.Status != ImportStatus.Cancelled)
            .OrderByDescending(job => job.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<ImportJob?> ClaimNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidateId = await dbContext.ImportJobs
                .Where(job => job.Status == ImportStatus.Pending)
                .OrderBy(job => job.CreatedAt)
                .Select(job => (Guid?)job.Id)
                .FirstOrDefaultAsync(cancellationToken);

            if (candidateId is null)
            {
                return null;
            }

            var rowsAffected = await dbContext.ImportJobs
                .Where(job => job.Id == candidateId && job.Status == ImportStatus.Pending)
                .ExecuteUpdateAsync(setters => setters.SetProperty(job => job.Status, ImportStatus.Processing), cancellationToken);

            if (rowsAffected == 1)
            {
                var claimedJob = await dbContext.ImportJobs
                    .FirstOrDefaultAsync(job => job.Id == candidateId, cancellationToken);

                if (claimedJob is not null)
                {
                    await dbContext.Entry(claimedJob).ReloadAsync(cancellationToken);
                }

                return claimedJob;
            }

            // Another caller claimed this job between the read and the update; retry with the next candidate.
        }

        return null;
    }

    public async Task<(IReadOnlyList<ImportJob> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        ImportStatus? status = null,
        string? fileName = null,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ImportJobs.AsQueryable().AsNoTracking();

        if (status is not null)
        {
            query = query.Where(job => job.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            query = query.Where(job => job.OriginalFileName.Contains(fileName));
        }

        if (createdFrom is not null)
        {
            query = query.Where(job => job.CreatedAt >= createdFrom);
        }

        if (createdTo is not null)
        {
            query = query.Where(job => job.CreatedAt <= createdTo);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(job => job.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<ImportJob[]> GetCompletedJobsByFileNamesAsync(string[] fileNames, CancellationToken cancellationToken = default)
    {
        return await dbContext.ImportJobs
            .AsNoTracking()
            .Where(job => job.Status == ImportStatus.Completed && fileNames.Contains(job.StoredFileName))
            .ToArrayAsync(cancellationToken);
    }

    public async Task<string[]> GetAllTrackedFileNamesAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.ImportJobs
            .AsNoTracking()
            .Select(job => job.StoredFileName)
            .ToArrayAsync(cancellationToken);
    }
}
