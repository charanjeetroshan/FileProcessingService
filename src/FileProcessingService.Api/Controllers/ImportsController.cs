using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using Microsoft.AspNetCore.Mvc;

namespace FileProcessingService.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController(
    IFileStorageService fileStorageService,
    IFileHasher fileHasher,
    IImportJobRepository importJobRepository,
    IImportErrorRepository importErrorRepository,
    ILogger<ImportsController> logger) : ControllerBase
{
    private const int MaxPageSize = 100;

    [HttpPost]
    [RequestSizeLimit(1000 * 1024 * 1024)] // Just an example limit of 1GB, the validation happens in the validator class.
    public async Task<ActionResult<ImportJobResponse>> Upload([FromForm] ImportJobRequest request, CancellationToken cancellationToken)
    {
        IFormFile file = request.File;

        string fileHash;
        await using (var hashStream = file.OpenReadStream())
        {
            fileHash = await fileHasher.ComputeHashAsync(hashStream, cancellationToken);
        }

        var existingJob = await importJobRepository.GetByFileHashAsync(fileHash, cancellationToken);
        if (existingJob is not null)
        {
            logger.LogInformation(
                "Rejected duplicate upload of file {OriginalFileName} matching existing job {ImportJobId} with hash {FileHash}",
                file.FileName, existingJob.Id, fileHash);

            return Conflict(ToResponse(existingJob));
        }

        await using var stream = file.OpenReadStream();
        var storedFileName = await fileStorageService.SaveAsync(file.FileName, stream, cancellationToken);

        var job = new ImportJob
        {
            OriginalFileName = file.FileName,
            StoredFileName = storedFileName,
            FileHash = fileHash,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await importJobRepository.AddAsync(job, cancellationToken);

        logger.LogInformation("Created import job {ImportJobId} for uploaded file {OriginalFileName}", job.Id, job.OriginalFileName);

        var response = ToResponse(job);
        return CreatedAtAction(nameof(GetById), new { id = job.Id }, response);
    }

    [HttpGet]
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
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportJobResponse>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var job = await importJobRepository.GetByIdAsync(id, cancellationToken);

        if (job is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(job));
    }

    [HttpGet("{id:guid}/errors")]
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
            Items = items.Select(ToResponse).ToList(),
            Page = page,
            PageSize = pageSize,
            TotalCount = totalCount
        };

        return Ok(response);
    }

    private static ImportJobResponse ToResponse(ImportJob job)
    {
        var completionTime = job.CompletedAt ?? (job.StartedAt is not null ? DateTimeOffset.UtcNow : null);

        return new ImportJobResponse
        {
            Id = job.Id,
            OriginalFileName = job.OriginalFileName,
            Status = job.Status.ToString(),
            TotalRows = job.TotalRows,
            ProcessedRows = job.ProcessedRows,
            SuccessfulRows = job.SuccessfulRows,
            FailedRows = job.FailedRows,
            PercentageComplete = job.TotalRows == 0 ? 0 : Math.Round(job.ProcessedRows / (double)job.TotalRows * 100, 2),
            CreatedAt = job.CreatedAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            ProcessingDuration = job.StartedAt is null ? null : completionTime - job.StartedAt,
            FailureReason = job.FailureReason
        };
    }

    private static ImportErrorResponse ToResponse(ImportError error) => new()
    {
        Id = error.Id,
        RowNumber = error.RowNumber,
        Field = error.Field,
        ErrorCode = error.ErrorCode,
        ErrorMessage = error.ErrorMessage,
        RawValue = error.RawValue
    };
}
