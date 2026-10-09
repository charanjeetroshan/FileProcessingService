using FileProcessingService.Domain.Enums;

namespace FileProcessingService.Domain.Entities;

public class ImportJob
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UploadedFileName { get; set; } = string.Empty;
    public string DataPackageGuid { get; set; } = string.Empty;
    public string CompositeHash { get; set; } = string.Empty;
    public ImportStatus Status { get; set; } = ImportStatus.Pending;
    public long TotalRows { get; set; }
    public long ProcessedRows { get; set; }
    public long SuccessfulRows { get; set; }
    public long FailedRows { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? FailureReason { get; set; }
}
