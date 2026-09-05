using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Exports;
using FileProcessingService.Domain.Exceptions;
using FileProcessingService.Infrastructure.FileStorage;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Net;

namespace FileProcessingService.Api.Controllers;

[ApiController]
[Route("api/exports")]
public class ExportsController(
    ICustomerRepository customerRepository,
    ILogger<ExportsController> logger,
    IEnumerable<IExporter> exporters,
    IOptions<FileStorageOptions> options
) : ControllerBase
{
    [HttpGet("{importId:guid}")]
    public async Task<IActionResult> ExportByImportId(
        Guid importId,
        [FromQuery] EExportFormat format,
        CancellationToken cancellationToken)
    {
        var customers = customerRepository.GetByImportIdAsync(importId, cancellationToken);

        if (customers is null || !await customers.AnyAsync(cancellationToken))
        {
            return NotFound();
        }

        var exporter = exporters.SingleOrDefault(e => e.Format == format)
            ?? throw new FileProcessingException(HttpStatusCode.BadRequest, $"No exporter is registered for format '{format}'.");

        var exportDirectory = options.Value.ExportDirectoryPath;

        logger.LogDebug("Exporting customers for Import Id: {ImportId}...", importId);

        ExportResult exportResult = await exporter.ExportCustomers(customers, exportDirectory, cancellationToken);

        logger.LogDebug("Export created for Import Id: {ImportId} at {FilePath}." + Environment.NewLine +
            "Row count: {RowCount}.", importId, exportResult.ExportedFilePath, exportResult.ExportedDataSetCount);

        return PhysicalFile(exportResult.ExportedFilePath, GetContentType(format), Path.GetFileName(exportResult.ExportedFilePath));
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
