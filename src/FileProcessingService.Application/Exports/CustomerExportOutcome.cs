using FileProcessingService.Application.Contracts;

namespace FileProcessingService.Application.Exports;

public abstract record CustomerExportOutcome
{
    private CustomerExportOutcome()
    {
    }

    public sealed record Exported(ExportResult Result, string ContentType) : CustomerExportOutcome;

    public sealed record JobNotFound : CustomerExportOutcome;

    public sealed record JobNotCompleted(ImportJobExportResponse Job) : CustomerExportOutcome;

    public sealed record NoCustomers(ImportJobExportResponse Job) : CustomerExportOutcome;
}
