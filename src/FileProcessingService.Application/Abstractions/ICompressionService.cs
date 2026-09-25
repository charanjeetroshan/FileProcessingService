namespace FileProcessingService.Application.Abstractions;

public interface ICompressionService
{
    public Task CompressAsync(string sourceDirectory, string destinationFilePath, CancellationToken cancellationToken);

    public Task DecompressAsync(string sourceFilePath, string destinationDirectory, CancellationToken cancellationToken);
}
