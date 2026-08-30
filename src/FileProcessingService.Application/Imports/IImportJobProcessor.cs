using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Imports;

public interface IImportJobProcessor
{
    Task ProcessJob(ImportJob job, CancellationToken cancellationToken);
}
