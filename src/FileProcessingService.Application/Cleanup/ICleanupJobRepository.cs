using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Cleanup;

public interface ICleanupJobRepository
{
    Task AddAsync(CleanupJob job, CancellationToken cancellationToken = default);

    Task AddJobsAsync(IEnumerable<CleanupJob> jobs, CancellationToken cancellationToken = default);

    Task<CleanupJob[]> ClaimByCountAsync(int count = 10, CancellationToken cancellationToken = default);

    Task<HashSet<string>> GetExistingCleanupFilePathsAsync(CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
