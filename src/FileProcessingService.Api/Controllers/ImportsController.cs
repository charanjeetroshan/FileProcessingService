using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessingService.Api.Controllers;

[ApiController]
[Route("api/imports")]
[Produces("application/json")]
public class ImportsController(
    IImportUploadService importUploadService,
    IImportJobRepository importJobRepository,
    IImportErrorRepository importErrorRepository) : ControllerBase
{
    private const int MaxPageSize = 100;
    private const int MaxUploadSizeInBytes = 1000 * 1024 * 1024; // Just an example limit of 1GB, the validation happens in the validator class.

    [HttpPost]
    [RequestSizeLimit(MaxUploadSizeInBytes)]
    [ProducesResponseType(typeof(ImportJobResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ImportJobResponse), StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ImportJobResponse>> Upload([FromForm] ImportJobRequest request, CancellationToken cancellationToken)
    {
        var result = await importUploadService.UploadAsync(request.File, cancellationToken);
        var response = result.Job.ToResponse();

        if (result.IsDuplicate)
        {
            return Conflict(response);
        }

        return CreatedAtAction(nameof(GetById), new { id = result.Job.Id }, response);
    }

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<ImportJobResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<ImportJobResponse>>> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] ImportStatus? status = null,
        [FromQuery] string? filename = null,
        [FromQuery(Name = "created-from")] DateTimeOffset? createdFrom = null,
        [FromQuery(Name = "created-to")] DateTimeOffset? createdTo = null,
        CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 1 : Math.Min(pageSize, MaxPageSize);

        var (items, totalCount) = await importJobRepository.GetPagedAsync(
            page, pageSize, status, filename, createdFrom, createdTo, cancellationToken);

        var response = new PagedResult<ImportJobResponse>
        {
            Items = items.Select(job => job.ToResponse()).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(ImportJobResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ImportJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await importJobRepository.GetByIdAsync(id, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(job.ToResponse());
    }

    [HttpGet("{id:guid}/errors")]
    [ProducesResponseType(typeof(PagedResult<ImportErrorResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<PagedResult<ImportErrorResponse>>> GetErrors(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? field = null,
        [FromQuery] string? errorCode = null,
        CancellationToken cancellationToken = default)
    {
        var job = await importJobRepository.GetByIdAsync(id, cancellationToken);
        if (job is null)
        {
            return NotFound();
        }

        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? 1 : Math.Min(pageSize, MaxPageSize);

        var (items, totalCount) = await importErrorRepository.GetByImportJobIdAsync(
            id, page, pageSize, field, errorCode, cancellationToken);

        var response = new PagedResult<ImportErrorResponse>
        {
            Items = items.Select(error => error.ToResponse()).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }
}
