using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Customers;
using FileProcessingService.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net;

namespace FileProcessingService.Application.Exports;

public class CustomerExportService(
    ICustomerRepository customerRepository,
    IEnumerable<IExporter> exporters,
    IOptions<FileStorageOptions> options,
    ILogger<CustomerExportService> logger) : ICustomerExportService
{
    public async Task<CustomerExportOutcome?> ExportByImportIdAsync(Guid importId, EExportFormat format, CancellationToken cancellationToken = default)
    {
        var customers = customerRepository.GetByImportIdAsync(importId, cancellationToken);

        await using var enumerator = customers.GetAsyncEnumerator(cancellationToken);
        if (!await enumerator.MoveNextAsync())
        {
            return null;
        }

        var exporter = exporters.SingleOrDefault(e => e.Format == format)
            ?? throw new FileProcessingException(HttpStatusCode.BadRequest, $"No exporter is registered for format '{format}'.");

        var exportDirectory = options.Value.ExportDirectoryPath;

        logger.LogInformation("Exporting customers for Import Id: {ImportId}...", importId);

        var exportResult = await exporter.ExportCustomers(Prepend(enumerator), exportDirectory, cancellationToken);

        logger.LogInformation("Export created for Import Id: {ImportId} at {FilePath}." + Environment.NewLine +
            "Row count: {RowCount}.", importId, exportResult.ExportedFilePath, exportResult.ExportedDataSetCount);

        return new CustomerExportOutcome(exportResult, GetContentType(format));
    }

    private static async IAsyncEnumerable<Domain.Entities.Customer> Prepend(IAsyncEnumerator<Domain.Entities.Customer> enumerator)
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
