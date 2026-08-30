namespace FileProcessingService.Infrastructure.FileStorage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    public string UploadDirectory { get; set; } = Path.Combine(AppContext.BaseDirectory, "uploads");
}
