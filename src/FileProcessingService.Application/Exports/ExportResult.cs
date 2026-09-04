namespace FileProcessingService.Application.Exports;

public record ExportResult(string ExportedFilePath, long ExportedDataSetCount);
