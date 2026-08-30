namespace FileProcessingService.Domain.Entities;

public class ImportError
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ImportJobId { get; set; }
    public long RowNumber { get; set; }
    public string? Field { get; set; }
    public string ErrorCode { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? RawValue { get; set; }
}
