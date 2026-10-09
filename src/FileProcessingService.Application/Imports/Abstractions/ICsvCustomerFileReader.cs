namespace FileProcessingService.Application.Imports.Abstractions;

public interface ICsvCustomerFileReader
{
    IAsyncEnumerable<CustomerImportRow> ReadAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
