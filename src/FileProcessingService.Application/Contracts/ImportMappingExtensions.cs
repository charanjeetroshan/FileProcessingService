using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Contracts;

public static class ImportMappingExtensions
{
    public static ImportJobResponse ToResponse(this ImportJob job)
    {
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
            ProcessingDuration = job.StartedAt is null || job.CompletedAt is null ? null : job.CompletedAt - job.StartedAt,
            FailureReason = job.FailureReason
        };
    }

    public static ImportErrorResponse ToResponse(this ImportError error) => new()
    {
        Id = error.Id,
        RowNumber = error.RowNumber,
        Field = error.Field,
        ErrorCode = error.ErrorCode,
        ErrorMessage = error.ErrorMessage,
        RawValue = error.RawValue
    };
}
