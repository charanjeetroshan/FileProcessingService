namespace FileProcessingService.Domain.Constants;

public class FileConstants
{
    public static readonly string[] AllowedExtensions = [".csv"];
    public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
}
