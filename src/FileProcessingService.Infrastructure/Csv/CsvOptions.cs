using System.ComponentModel.DataAnnotations;

namespace FileProcessingService.Infrastructure.Csv;

public class CsvOptions
{
    public const string SectionName = "CsvOptions";

    [Required(AllowEmptyStrings = false, ErrorMessage = $"{nameof(Separator)} is not configured. Check appsettings.json.")]
    public string Separator { get; set; } = ",";
}
