namespace FileProcessingService.Application.Abstractions;

public interface ICompressor
{
    public Task CompressAsync(string sourceDirectory, string destinationFilePath, CancellationToken cancellationToken);

    public Task DecompressAsync(string sourceFilePath, string destinationDirectory, CancellationToken cancellationToken);
}
