using FileProcessingService.Domain.Enums;

namespace FileProcessingService.Domain.Entities;

public class CleanupJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string FilePath { get; set; } = string.Empty;
    public CleanupStatus Status { get; set; } = CleanupStatus.Pending;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}
