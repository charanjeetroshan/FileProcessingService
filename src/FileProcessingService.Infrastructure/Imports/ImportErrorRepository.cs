using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FileProcessingService.Infrastructure.Imports;

public class ImportErrorRepository(FileProcessingDbContext dbContext) : IImportErrorRepository
{
    public async Task<(IReadOnlyList<ImportError> Items, int TotalCount)> GetByImportJobIdAsync(
        Guid importJobId,
        int page,
        int pageSize,
        string? field = null,
        string? errorCode = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.ImportErrors
            .Where(error => error.ImportJobId == importJobId);

        if (!string.IsNullOrWhiteSpace(field))
        {
            query = query.Where(error => error.Field == field);
        }

        if (!string.IsNullOrWhiteSpace(errorCode))
        {
            query = query.Where(error => error.ErrorCode == errorCode);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(error => error.RowNumber)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }
}
