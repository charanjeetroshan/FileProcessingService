using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;

namespace FileProcessingService.Application.Imports.Abstractions;

public interface IImportJobRepository
{
    Task AddAsync(ImportJob job, CancellationToken cancellationToken = default);

    Task<ImportJob?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ImportJob?> GetByHashAsync(string fileHash, CancellationToken cancellationToken = default);

    Task<ImportJob?> ClaimNextPendingJobAsync(CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<ImportJob> Items, int TotalCount)> GetPagedAsync(
        int page,
        int pageSize,
        ImportStatus? status = null,
        string? fileName = null,
        DateTimeOffset? createdFrom = null,
        DateTimeOffset? createdTo = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
