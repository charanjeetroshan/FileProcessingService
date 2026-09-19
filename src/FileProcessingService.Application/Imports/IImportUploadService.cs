using FileProcessingService.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace FileProcessingService.Application.Imports;

public interface IImportUploadService
{
    Task<ImportUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public record ImportUploadResult(ImportJob Job, bool IsDuplicate);
