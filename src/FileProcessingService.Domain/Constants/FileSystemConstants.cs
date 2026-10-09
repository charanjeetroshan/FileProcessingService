namespace FileProcessingService.Domain.Constants;

public class FileSystemConstants
{
    public const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50MB
    public const long MaxDirectorySizeBytes = 100 * 1024 * 1024; // 100MB
    public const short MaxFileNumber = 2;

    public static readonly string[] AllowedUploadFileExtensions = [".zip"];
    public const string CustomerFileName = "customers.csv";
    public static readonly string[] AllowedFileNames = [CustomerFileName];


    public const string ArchiveDirectoryName = "archive";
    public const string DataDirectoryName = "data";
}
