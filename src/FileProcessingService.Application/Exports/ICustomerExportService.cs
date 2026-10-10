namespace FileProcessingService.Application.Exports;

public interface ICustomerExportService
{
    Task<CustomerExportOutcome> ExportByImportIdAsync(Guid importId, EExportFormat format, CancellationToken cancellationToken = default);
}

