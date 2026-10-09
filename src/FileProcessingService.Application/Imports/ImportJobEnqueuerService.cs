using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain;
using FileProcessingService.Domain.Constants;
using FileProcessingService.Domain.Entities;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Immutable;
using System.Text;

namespace FileProcessingService.Application.Imports;

public class ImportJobEnqueuerService(
    IFileStorageService fileStorageService,
    IFileHasher fileHasher,
    IImportJobRepository importJobRepository,
    IOptions<FileStorageOptions> fileStorageOptions,
    ILogger<ImportJobEnqueuerService> logger,
    ICompressionService compression) : IImportJobEnqueuerService
{
    public async Task<ImportUploadResult> EnqueueAsync(IFormFile file, CancellationToken cancellationToken = default)
    {
        string uploadParentDirectory = string.Empty;
        try
        {
            string uploadDirectory = fileStorageOptions.Value.UploadDirectoryPath;
            logger.LogDebug("Root upload directory: {UploadDirectory}", uploadDirectory);

            logger.LogInformation("Saving uploaded file {FileName} to {UploadDirectory}", file.FileName, uploadDirectory);
            await using var stream = file.OpenReadStream();
            string savedFilePath = await fileStorageService.SaveAsync(file.FileName, uploadDirectory, stream, cancellationToken);

            string dataPackageGuid = Utilities.GenerateDataPackageGuid();
            uploadParentDirectory = Path.Combine(uploadDirectory, dataPackageGuid);

            string decompressDataDir = Path.Combine(uploadParentDirectory, FileSystemConstants.DataDirectoryName);
            logger.LogInformation("Decompressing file {Archive} to {DecompressDataDirectory}", savedFilePath, decompressDataDir);
            await compression.DecompressAsync(savedFilePath, decompressDataDir, cancellationToken);

            MoveUploadedArchiveFile(savedFilePath, uploadParentDirectory, file.FileName);

            ImmutableArray<string> validatedFilePaths = GetValidFilePaths(decompressDataDir);
            string hash = await GenerateCompositeHashAsync(validatedFilePaths, cancellationToken);

            var existingJob = await importJobRepository.GetByHashAsync(hash, cancellationToken);
            if (existingJob is not null)
            {
                logger.LogWarning(
                    "Rejected duplicate upload of file {OriginalFileName} matching existing job {ImportJobId} with hash {FileHash}",
                    file.FileName, existingJob.Id, hash);

                CleanupDecompressedDirectory(uploadParentDirectory);

                return new ImportUploadResult(existingJob, IsDuplicate: true);
            }

            var job = new ImportJob
            {
                UploadedFileName = file.FileName,
                DataPackageGuid = dataPackageGuid,
                CompositeHash = hash,
                CreatedAt = DateTimeOffset.UtcNow
            };

            await importJobRepository.AddAsync(job, cancellationToken);
            logger.LogInformation("Created import job {ImportJobId} for uploaded file {OriginalFileName}", job.Id, job.UploadedFileName);

            return new ImportUploadResult(job, IsDuplicate: false);
        }
        catch (Exception ex)
        {
            if (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "An error occurred while enqueuing the import job for file {OriginalFileName}", file.FileName);
            }

            if (fileStorageService.DirectoryExists(uploadParentDirectory))
            {
                CleanupDecompressedDirectory(uploadParentDirectory);
            }

            throw;
        }
    }

    private void MoveUploadedArchiveFile(string archiveFilePath, string uploadParentDirectory, string fileName)
    {
        string destinationFilePath = Path.Combine(uploadParentDirectory, FileSystemConstants.ArchiveDirectoryName, fileName);
        fileStorageService.MoveFile(archiveFilePath, destinationFilePath);
    }

    private ImmutableArray<string> GetValidFilePaths(string directoryPath)
    {
        long attemptedSizeMB = fileStorageService.GetDirectorySize(directoryPath) / 1024 / 1024;

        if (attemptedSizeMB > FileSystemConstants.MaxDirectorySizeBytes / 1024 / 1024)
        {
            long allowedSizeMB = FileSystemConstants.MaxDirectorySizeBytes / 1024 / 1024;

            throw new ValidationException(
                [new ValidationFailure("File.ContentSize", $"Uploaded file content exceeds maximum allowed size. Allowed max: {allowedSizeMB} MB, Attempted: {attemptedSizeMB} MB")]);
        }

        string[] filePaths = fileStorageService.GetFilesAt(directoryPath);
        if (filePaths.Length == 0)
        {
            throw new ValidationException([new ValidationFailure("File.Length", "Uploaded file contains no files.")]);
        }

        if (filePaths.Length > FileSystemConstants.MaxFileNumber)
        {
            throw new ValidationException(
                [new ValidationFailure("File.Length", $"Uploaded file contains more than the maximum allowed number of files ({FileSystemConstants.MaxFileNumber}).")]);
        }

        var validFilePaths = ImmutableArray.CreateBuilder<string>();
        foreach (var filePath in filePaths)
        {
            string fileName = Path.GetFileName(filePath);

            if (FileSystemConstants.AllowedFileNames.Contains(fileName, StringComparer.OrdinalIgnoreCase))
            {
                validFilePaths.Add(filePath);
            }
        }

        if (validFilePaths.Count == 0)
        {
            throw new ValidationException(
                [new ValidationFailure("File.Content", $"Uploaded file contains no valid files. Valid file names are: [{string.Join(", ", FileSystemConstants.AllowedFileNames)}]")]);
        }

        return validFilePaths.ToImmutable();
    }

    private async Task<string> GenerateCompositeHashAsync(ImmutableArray<string> filePaths, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();

        foreach (var filePath in filePaths.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase))
        {
            await using FileStream fileStream = fileStorageService.OpenFile(filePath);

            if (fileStream.Length == 0)
            {
                logger.LogWarning("Skipping empty file {FilePath} during composite hash generation", filePath);
                continue;
            }

            string fileHash = await fileHasher.ComputeHashAsync(fileStream, cancellationToken);

            builder.Append(Path.GetFileName(filePath).ToUpperInvariant());
            builder.Append(':');
            builder.Append(fileHash);
            builder.Append('|');
        }

        if (builder.Length == 0)
        {
            throw new ValidationException([new ValidationFailure("File.Length", "Uploaded file contains only empty files.")]);
        }

        return await fileHasher.ComputeHashAsync(builder.ToString(), cancellationToken);
    }

    private void CleanupDecompressedDirectory(string directoryPath)
    {
        fileStorageService.DeleteDirectory(directoryPath);
    }
}
