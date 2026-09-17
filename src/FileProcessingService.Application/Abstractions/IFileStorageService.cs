namespace FileProcessingService.Application.Abstractions;

public interface IFileStorageService
{
    Task<string> SaveAsync(string fileName, string rootDirectoryPath, Stream content, CancellationToken cancellationToken);

    string[] GetFilesAt(string directoryPath);

    void DeleteFile(string filepath);
}
