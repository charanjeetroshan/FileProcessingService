using CsvHelper;
using CsvHelper.Configuration;
using FileProcessingService.Application.Exports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Csv;
using System.Globalization;

namespace FileProcessingService.Infrastructure.Exports;

public class CsvExporter : IExporter
{
    public EExportFormat Format => EExportFormat.Csv;

    public async Task<ExportResult> ExportCustomers(IAsyncEnumerable<Customer> customers, string exportDestination, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(exportDestination);

        var filePath = Path.Combine(exportDestination, $"customers_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.csv");

        await using var writer = new StreamWriter(filePath);

        using var csvWriter = new CsvWriter(writer, new CsvConfiguration(CultureInfo.InvariantCulture));
        csvWriter.Context.RegisterClassMap<CustomerExportRowMap>();

        csvWriter.WriteHeader<CustomerExportRow>();
        await csvWriter.NextRecordAsync();

        long exportedCustomerCount = 0;

        await foreach (var customer in customers.WithCancellation(cancellationToken))
        {
            exportedCustomerCount++;
            csvWriter.WriteRecord(customer);
            await csvWriter.NextRecordAsync();
        }

        return new ExportResult(filePath, exportedCustomerCount);
    }
}
