using FileProcessingService.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace FileProcessingService.Application.Imports.Abstractions;

public interface IImportJobEnqueuerService
{
    Task<ImportUploadResult> EnqueueAsync(IFormFile file, CancellationToken cancellationToken = default);
}

public record ImportUploadResult(ImportJob Job, bool IsDuplicate);
public record DecompressResult(string UploadParentDirectory, string DecompressedDataDirectory);