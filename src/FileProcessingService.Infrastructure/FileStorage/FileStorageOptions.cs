using System.ComponentModel.DataAnnotations;

namespace FileProcessingService.Infrastructure.FileStorage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(UploadDirectoryPath)} is not configured. Check appsettings.json.")]
    public string UploadDirectoryPath { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(ExportDirectoryPath)} is not configured. Check appsettings.json.")]
    public string ExportDirectoryPath { get; set; } = string.Empty;
}
