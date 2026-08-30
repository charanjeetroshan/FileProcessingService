namespace FileProcessingService.Application.Contracts;

public class ImportErrorResponse
{
    public Guid Id { get; init; }
    public long RowNumber { get; init; }
    public string? Field { get; init; }
    public string ErrorCode { get; init; } = string.Empty;
    public string ErrorMessage { get; init; } = string.Empty;
    public string? RawValue { get; init; }
}
