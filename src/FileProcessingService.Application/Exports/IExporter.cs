using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Exports;

public interface IExporter
{
    EExportFormat Format { get; }
    Task<ExportResult> ExportCustomers(IAsyncEnumerable<Customer> customers, string exportDestination, CancellationToken cancellationToken = default);
}
