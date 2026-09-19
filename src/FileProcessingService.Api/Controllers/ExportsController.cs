using FileProcessingService.Application.Exports;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessingService.Api.Controllers;

[ApiController]
[Route("api/exports")]
public class ExportsController(ICustomerExportService customerExportService) : ControllerBase
{
    [HttpGet("{importId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportByImportId(
        Guid importId,
        [FromQuery] EExportFormat format,
        CancellationToken cancellationToken)
    {
        var outcome = await customerExportService.ExportByImportIdAsync(importId, format, cancellationToken);

        if (outcome is null)
        {
            return NotFound();
        }

        return PhysicalFile(outcome.Result.ExportedFilePath, outcome.ContentType, Path.GetFileName(outcome.Result.ExportedFilePath));
    }
}
