using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain.Constants;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Globalization;

namespace FileProcessingService.Infrastructure.Imports;

internal class ImportJobProcessor(
    IOptions<FileStorageOptions> storageOptions,
    IFileStorageService fileStorageService,
    ICsvCustomerFileReader csvReader,
    IValidator<CustomerImportRow> csvRowValidator,
    FileProcessingDbContext db,
    ILogger<ImportJobProcessor> logger
) : IImportJobProcessor
{
    private const int BatchSize = 1000;

    public async Task ProcessJob(ImportJob job, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting import job {ImportJobId} for data package {DataPackage}", job.Id, job.DataPackageGuid);

        job.Status = ImportStatus.Processing;
        job.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        List<Customer> customers = [];
        List<ImportError> errors = [];

        string dataDirectoryPath = Path.Combine(storageOptions.Value.UploadDirectoryPath, job.DataPackageGuid, FileSystemConstants.DataDirectoryName);
        var uploadedFilePaths = fileStorageService.GetFilesAt(dataDirectoryPath);

        foreach (var filePath in uploadedFilePaths)
        {
            try
            {
                await using FileStream csvStream = fileStorageService.OpenFile(filePath);

                if (Path.GetFileName(filePath).Equals(FileSystemConstants.CustomerFileName, StringComparison.OrdinalIgnoreCase))
                {
                    logger.LogInformation("Processing customer file {FilePath} for import job {ImportJobId}", filePath, job.Id);
                    await ProcessCustomerFile(csvStream, job, customers, errors, cancellationToken);
                }
                else
                {
                    logger.LogWarning("Skipping unrecognized file {FilePath} for import job {ImportJobId}", filePath, job.Id);
                }

                await Flush(customers, errors, cancellationToken);

                job.Status = ImportStatus.Completed;
                job.CompletedAt = DateTimeOffset.UtcNow;

                await db.SaveChangesAsync(cancellationToken);

                logger.LogInformation(
                    "Completed import job {ImportJobId}: {ProcessedRows} processed, {SuccessfulRows} succeeded, {FailedRows} failed",
                    job.Id, job.ProcessedRows, job.SuccessfulRows, job.FailedRows);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning("Import job {ImportJobId} was cancelled after processing {ProcessedRows} rows", job.Id, job.ProcessedRows);

                job.Status = ImportStatus.Cancelled;
                job.FailureReason = "Job processing was cancelled.";
                job.CompletedAt = DateTimeOffset.UtcNow;

                await db.SaveChangesAsync(CancellationToken.None);
                await RemovePartialImportDataAsync(job.Id);
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Import job {ImportJobId} failed after processing {ProcessedRows} rows", job.Id, job.ProcessedRows);

                job.Status = ImportStatus.Failed;
                job.FailureReason = ex.Message;
                job.CompletedAt = DateTimeOffset.UtcNow;

                await db.SaveChangesAsync(CancellationToken.None);
                await RemovePartialImportDataAsync(job.Id);
            }
        }
    }

    private async Task ProcessCustomerFile(Stream csvStream, ImportJob job, List<Customer> customers, List<ImportError> errors, CancellationToken cancellationToken)
    {
        await foreach (CustomerImportRow row in csvReader.ReadAsync(csvStream, cancellationToken))
        {
            cancellationToken.ThrowIfCancellationRequested();

            job.TotalRows++;

            ValidationResult validationResult = await csvRowValidator.ValidateAsync(row, cancellationToken);

            if (!validationResult.IsValid)
            {
                foreach (var validationError in validationResult.Errors)
                {
                    errors.Add(new ImportError
                    {
                        ImportJobId = job.Id,
                        RowNumber = row.RowNumber,
                        Field = validationError.PropertyName,
                        ErrorCode = validationError.ErrorCode,
                        ErrorMessage = validationError.ErrorMessage,
                        RawValue = validationError.AttemptedValue?.ToString()
                    });
                }

                job.FailedRows++;
                job.ProcessedRows++;

                await FlushIfNeeded(customers, errors, cancellationToken);
                continue;
            }

            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                ImportId = job.Id,
                FirstName = row.FirstName?.Trim() ?? string.Empty,
                LastName = row.LastName?.Trim() ?? string.Empty,
                Email = row.Email?.Trim() ?? string.Empty,
                DateOfBirth = DateOnly.Parse(row.DateOfBirth!, CultureInfo.InvariantCulture),
                Country = row.Country?.Trim() ?? string.Empty,
                CreatedAt = DateTimeOffset.UtcNow
            };

            customers.Add(customer);
            job.SuccessfulRows++;
            job.ProcessedRows++;

            await FlushIfNeeded(customers, errors, cancellationToken);
        }
    }

    private async Task FlushIfNeeded(List<Customer> customers, List<ImportError> errors, CancellationToken cancellationToken)
    {
        if (customers.Count + errors.Count >= BatchSize)
        {
            await Flush(customers, errors, cancellationToken);
        }
    }

    private async Task Flush(List<Customer> customers, List<ImportError> errors, CancellationToken cancellationToken)
    {
        if (customers.Count == 0 && errors.Count == 0)
        {
            return;
        }

        if (customers.Count > 0)
        {
            await db.Customers.AddRangeAsync(customers, cancellationToken);
        }

        if (errors.Count > 0)
        {
            await db.ImportErrors.AddRangeAsync(errors, cancellationToken);
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Flushed batch of {CustomerCount} customers and {ErrorCount} errors", customers.Count, errors.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to flush batch for import job.");

            foreach (var customer in customers)
            {
                db.Entry(customer).State = EntityState.Detached;
            }

            foreach (var error in errors)
            {
                db.Entry(error).State = EntityState.Detached;
            }

            throw;
        }
        finally
        {
            customers.Clear();
            errors.Clear();
        }
    }

    private async Task RemovePartialImportDataAsync(Guid jobId)
    {
        logger.LogError("Removing partial import data for job {JobId} because of an error.", jobId);
        using var cleanupCts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await db.Customers.Where(c => c.ImportId == jobId).ExecuteDeleteAsync(cleanupCts.Token);
        await db.ImportErrors.Where(c => c.ImportJobId == jobId).ExecuteDeleteAsync(cleanupCts.Token);
    }
}

