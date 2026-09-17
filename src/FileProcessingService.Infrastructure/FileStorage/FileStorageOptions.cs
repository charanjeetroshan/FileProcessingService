using System.ComponentModel.DataAnnotations;

namespace FileProcessingService.Infrastructure.FileStorage;

public class FileStorageOptions
{
    public const string SectionName = "FileStorage";

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(UploadDirectoryPath)} is not configured. Check appsettings.json.")]
    public string UploadDirectoryPath { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(ExportDirectoryPath)} is not configured. Check appsettings.json.")]
    public string ExportDirectoryPath { get; set; } = string.Empty;

    [Range(1, 30, ErrorMessage = $"{nameof(FileKeepDurationInDays)} must be between 1 and 30. Check appsettings.json.")]
    public short FileKeepDurationInDays { get; set; }
}
