using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileProcessingService.Application.Exports;

public class CustomerExportService(
    IImportJobRepository importJobRepository,
    ICustomerRepository customerRepository,
    IEnumerable<IExporter> exporters,
    IOptions<FileStorageOptions> options,
    ILogger<CustomerExportService> logger) : ICustomerExportService
{
    public async Task<CustomerExportOutcome> ExportByImportIdAsync(Guid importId, EExportFormat format, CancellationToken cancellationToken = default)
    {
        var importJob = await importJobRepository.GetByIdAsync(importId, cancellationToken);

        if (importJob is null)
        {
            return new CustomerExportOutcome.JobNotFound();
        }

        if (importJob.Status != ImportStatus.Completed)
        {
            return new CustomerExportOutcome.JobNotCompleted(importJob.ToExportResponse());
        }

        var customers = customerRepository.GetByImportIdAsync(importId, cancellationToken);

        await using var enumerator = customers.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync())
        {
            return new CustomerExportOutcome.NoCustomers(importJob.ToExportResponse());
        }

        var exporter = exporters.SingleOrDefault(e => e.Format == format)
            ?? throw new InvalidOperationException($"No exporter is registered for format '{format}'.");

        var exportDirectory = options.Value.ExportDirectoryPath;

        logger.LogInformation("Exporting customers for Import Id: {ImportId}...", importId);

        var exportResult = await exporter.ExportCustomers(Prepend(enumerator), exportDirectory, cancellationToken);

        logger.LogInformation("Export created for Import Id: {ImportId} at {FilePath}." + Environment.NewLine +
            "Row count: {RowCount}.", importId, exportResult.ExportedFilePath, exportResult.ExportedDataSetCount);

        return new CustomerExportOutcome.Exported(exportResult, GetContentType(format));
    }

    private static async IAsyncEnumerable<Customer> Prepend(IAsyncEnumerator<Customer> enumerator)
    {
        yield return enumerator.Current;

        while (await enumerator.MoveNextAsync())
        {
            yield return enumerator.Current;
        }
    }

    private static string GetContentType(EExportFormat format)
    {
        return format switch
        {
            EExportFormat.NdJson => "application/x-ndjson",
            EExportFormat.Csv => "text/csv",
            _ => throw new ArgumentOutOfRangeException(nameof(format), $"Unsupported export format: {format}")
        };
    }
}
