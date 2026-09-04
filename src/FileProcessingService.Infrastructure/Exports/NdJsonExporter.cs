using FileProcessingService.Application.Exports;
using FileProcessingService.Domain.Entities;
using System.Text.Json;

namespace FileProcessingService.Infrastructure.Exports;

public class NdJsonExporter : IExporter
{
    public EExportFormat Format => EExportFormat.NdJson;

    public async Task<ExportResult> ExportCustomers(IAsyncEnumerable<Customer> customers, string exportDestination, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(exportDestination);

        var filePath = Path.Combine(exportDestination, $"customers_{DateTime.UtcNow:yyyyMMddHHmmss}_{Guid.NewGuid():N}.ndjson");

        await using var writer = new StreamWriter(filePath);
        long exportedCustomerCount = 0;

        await foreach (var customer in customers.WithCancellation(cancellationToken))
        {
            exportedCustomerCount++;
            var json = JsonSerializer.Serialize(customer);
            await writer.WriteLineAsync(json.AsMemory(), cancellationToken);
        }

        return new ExportResult(filePath, exportedCustomerCount);
    }
}
