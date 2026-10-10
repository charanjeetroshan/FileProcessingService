using FileProcessingService.Application.Exports;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessingService.Api.Controllers;

[ApiController]
[Route("api/exports")]
public class ExportsController(ICustomerExportService customerExportService) : ControllerBase
{
    [HttpGet("{importId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ExportByImportId(
        Guid importId,
        [FromQuery] EExportFormat format,
        CancellationToken cancellationToken)
    {
        var outcome = await customerExportService.ExportByImportIdAsync(importId, format, cancellationToken);

        return outcome switch
        {
            CustomerExportOutcome.Exported exported => PhysicalFile(exported.Result.ExportedFilePath, exported.ContentType, Path.GetFileName(exported.Result.ExportedFilePath)),
            CustomerExportOutcome.NoCustomers => NoContent(),
            CustomerExportOutcome.JobNotFound => NotFound(CreateProblem(StatusCodes.Status404NotFound, "Import job not found.")),
            CustomerExportOutcome.JobNotCompleted notCompleted => BadRequest(CreateProblem(StatusCodes.Status400BadRequest, "Invalid job status, no export possible.", notCompleted.Job)),
            _ => throw new InvalidOperationException($"Unhandled export outcome: {outcome.GetType().Name}")
        };
    }

    private static ProblemDetails CreateProblem(int status, string detail, object? job = null)
    {
        var problem = new ProblemDetails
        {
            Title = "Error occurred",
            Detail = detail,
            Status = status
        };

        if (job is not null)
        {
            problem.Extensions["job"] = job;
        }

        return problem;
    }
}
