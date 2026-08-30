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

    public async Task<ImportJob?> GetByFileHashAsync(string fileHash, CancellationToken cancellationToken = default)
    {
        var candidates = await dbContext.ImportJobs
            .Where(job => job.FileHash == fileHash
                && job.Status != ImportStatus.Failed
                && job.Status != ImportStatus.Cancelled)
            .ToListAsync(cancellationToken);

        return candidates
            .OrderByDescending(job => job.CreatedAt)
            .FirstOrDefault();
    }

    public async Task<ImportJob?> ClaimNextPendingJobAsync(CancellationToken cancellationToken = default)
    {
        const int maxAttempts = 5;

        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var pendingCandidates = await dbContext.ImportJobs
                .Where(job => job.Status == ImportStatus.Pending)
                .Select(job => new { job.Id, job.CreatedAt })
                .ToListAsync(cancellationToken);

            var candidateId = pendingCandidates
                .OrderBy(job => job.CreatedAt)
                .Select(job => (Guid?)job.Id)
                .FirstOrDefault();

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
        var query = dbContext.ImportJobs.AsQueryable();

        if (status is not null)
        {
            query = query.Where(job => job.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(fileName))
        {
            query = query.Where(job => job.OriginalFileName.Contains(fileName));
        }

        // CreatedAt (DateTimeOffset) comparisons and ordering cannot be translated to SQL by the
        // SQLite provider, so the filtered set is materialized first and range-filtered/ordered
        // client-side instead.
        var totalCandidates = await query.ToListAsync(cancellationToken);

        var filtered = totalCandidates
            .Where(job => createdFrom is null || job.CreatedAt >= createdFrom)
            .Where(job => createdTo is null || job.CreatedAt <= createdTo)
            .ToList();

        var totalCount = filtered.Count;

        var items = filtered
            .OrderByDescending(job => job.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return (items, totalCount);
    }
}
