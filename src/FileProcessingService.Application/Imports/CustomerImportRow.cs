namespace FileProcessingService.Application.Imports;

public record CustomerImportRow
{
    public long RowNumber { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? DateOfBirth { get; init; }
    public string? Country { get; init; }
}
