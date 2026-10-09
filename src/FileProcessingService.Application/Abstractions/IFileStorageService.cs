namespace FileProcessingService.Application.Abstractions;

public interface IFileStorageService
{
    FileStream OpenFile(string filePath);

    Task<string> SaveAsync(string fileName, string rootDirectoryPath, Stream content, CancellationToken cancellationToken);

    string[] GetFilesAt(string directoryPath);

    void DeleteFile(string filepath);

    void DeleteDirectory(string directoryPath);

    bool DirectoryExists(string directoryPath);

    void MoveFile(string sourceFilePath, string destinationFilePath);

    long GetDirectorySize(string path);
}
