using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Configuration;
using FileProcessingService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileProcessingService.Application.Imports;

public class ImportUploadService(
    IFileStorageService fileStorageService,
    IFileHasher fileHasher,
    IImportJobRepository importJobRepository,
    IOptions<FileStorageOptions> fileStorageOptions,
    ILogger<ImportUploadService> logger) : IImportUploadService
{
    public async Task<ImportUploadResult> UploadAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        string fileHash;
        await using (var hashStream = file.OpenReadStream())
        {
            fileHash = await fileHasher.ComputeHashAsync(hashStream, cancellationToken);
        }

        var existingJob = await importJobRepository.GetByFileHashAsync(fileHash, cancellationToken);
        if (existingJob is not null)
        {
            logger.LogInformation(
                "Rejected duplicate upload of file {OriginalFileName} matching existing job {ImportJobId} with hash {FileHash}",
                file.FileName, existingJob.Id, fileHash);

            return new ImportUploadResult(existingJob, IsDuplicate: true);
        }

        await using var stream = file.OpenReadStream();
        var uploadDirectory = fileStorageOptions.Value.UploadDirectoryPath;
        logger.LogDebug("Root upload directory: {UploadDirectory}", uploadDirectory);
        var storedFileName = await fileStorageService.SaveAsync(file.FileName, uploadDirectory, stream, cancellationToken);

        var job = new ImportJob
        {
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            FileHash = fileHash,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await importJobRepository.AddAsync(job, cancellationToken);

        logger.LogInformation("Created import job {ImportJobId} for uploaded file {OriginalFileName}", job.Id, job.OriginalFileName);

        return new ImportUploadResult(job, IsDuplicate: false);
    }
}
