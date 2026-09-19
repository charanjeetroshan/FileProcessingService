using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Imports;
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
    ICsvCustomerFileReader csvReader,
    IValidator<CustomerImportRow> csvRowValidator,
    FileProcessingDbContext db,
    ILogger<ImportJobProcessor> logger
) : IImportJobProcessor
{
    private const int BatchSize = 1000;

    public async Task ProcessJob(ImportJob job, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting import job {ImportJobId} for file {StoredFileName}", job.Id, job.StoredFileName);

        job.Status = ImportStatus.Processing;
        job.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        List<Customer> customers = [];
        List<ImportError> errors = [];

        var filePath = Path.Combine(storageOptions.Value.UploadDirectoryPath, job.StoredFileName);

        try
        {
            await using FileStream csvStream = File.OpenRead(filePath);

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

            try
            {
                await Flush(customers, errors, CancellationToken.None);
            }
            catch (Exception flushEx)
            {
                logger.LogWarning(flushEx, "Failed to flush final batch while cancelling import job {ImportJobId}", job.Id);
            }

            job.Status = ImportStatus.Cancelled;
            job.FailureReason = "Job processing was cancelled.";
            job.CompletedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Import job {ImportJobId} failed after processing {ProcessedRows} rows", job.Id, job.ProcessedRows);

            job.Status = ImportStatus.Failed;
            job.FailureReason = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;

            await db.SaveChangesAsync(CancellationToken.None);
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

        logger.LogDebug("Flushed batch of {CustomerCount} customers and {ErrorCount} errors", customers.Count, errors.Count);
    }
}

