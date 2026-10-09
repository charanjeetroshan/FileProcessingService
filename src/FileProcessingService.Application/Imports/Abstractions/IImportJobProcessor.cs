using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Imports.Abstractions;

public interface IImportJobProcessor
{
    Task ProcessJob(ImportJob job, CancellationToken cancellationToken);
}
