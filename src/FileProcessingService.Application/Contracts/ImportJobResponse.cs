namespace FileProcessingService.Application.Contracts;

public class ImportJobResponse
{
    public Guid Id { get; init; }
    public string OriginalFileName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public long TotalRows { get; init; }
    public long ProcessedRows { get; init; }
    public long SuccessfulRows { get; init; }
    public long FailedRows { get; init; }
    public double PercentageComplete { get; init; }
    public DateTimeOffset CreatedAt { get; init; }
    public DateTimeOffset? StartedAt { get; init; }
    public DateTimeOffset? CompletedAt { get; init; }
    public TimeSpan? ProcessingDuration { get; init; }
    public string? FailureReason { get; init; }
}
