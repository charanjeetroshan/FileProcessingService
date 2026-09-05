namespace FileProcessingService.Application.Abstractions;

public interface IFileStorageService
{
    Task<string> SaveAsync(string fileName, string rootDirectoryPath, Stream content, CancellationToken cancellationToken);
}
