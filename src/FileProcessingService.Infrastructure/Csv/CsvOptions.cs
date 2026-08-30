namespace FileProcessingService.Infrastructure.Csv;

public class CsvOptions
{
    public const string SectionName = "CsvOptions";

    public string Separator { get; set; } = ",";
}
