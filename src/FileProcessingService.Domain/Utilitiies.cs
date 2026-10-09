namespace FileProcessingService.Domain;

public static class Utilities
{
    public static string GenerateDataPackageGuid() => Guid.NewGuid().ToString("N").ToUpperInvariant();
}
