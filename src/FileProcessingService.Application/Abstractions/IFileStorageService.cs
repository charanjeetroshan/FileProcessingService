namespace FileProcessingService.Application.Abstractions;

public interface IFileStorageService
{
    Task<string> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default);

    Task<Stream> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default);
}
