using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Imports;

public interface IImportErrorRepository
{
    Task<(IReadOnlyList<ImportError> Items, int TotalCount)> GetByImportJobIdAsync(
        Guid importJobId,
        int page,
        int pageSize,
        string? field = null,
        string? errorCode = null,
        CancellationToken cancellationToken = default);
}
