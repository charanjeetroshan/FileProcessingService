namespace FileProcessingService.Application.Exports;

public record CustomerExportRow
{
    public string Id { get; init; } = string.Empty;
    public string ImportId { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string DateOfBirth { get; init; } = string.Empty;
    public string Country { get; init; } = string.Empty;
}
