namespace FileProcessingService.Application.Imports;

public interface ICsvCustomerFileReader
{
    IAsyncEnumerable<CustomerImportRow> ReadAsync(Stream csvStream, CancellationToken cancellationToken = default);
}
