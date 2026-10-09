using FileProcessingService.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FileProcessingService.Infrastructure.FileStorage;

public class LocalFileStorageService(ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    public FileStream OpenFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"File {filePath} does not exist");
        }

        logger.LogInformation("Opening file {FilePath}", filePath);
        return File.OpenRead(filePath);
    }

    public async Task<string> SaveAsync(string fileName, string rootDirectoryPath, Stream content, CancellationToken cancellationToken)
    {
        // Ensure the root directory exists
        Directory.CreateDirectory(rootDirectoryPath);

        var fullPath = Path.Combine(rootDirectoryPath, fileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        logger.LogInformation("Saved uploaded file {FileName}", fileName);

        return fullPath;
    }

    public void DeleteFile(string filePath)
    {
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
            logger.LogInformation("Deleted file {FilePath}", filePath);
        }
        else
        {
            throw new FileNotFoundException($"File or directory {filePath} does not exist");
        }
    }

    public void DeleteDirectory(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            Directory.Delete(directoryPath, recursive: true);
            logger.LogInformation("Deleted directory {DirectoryPath}", directoryPath);
        }
        else
        {
            throw new DirectoryNotFoundException($"Directory {directoryPath} does not exist");
        }
    }

    public bool DirectoryExists(string directoryPath)
    {
        return Directory.Exists(directoryPath);
    }

    public string[] GetFilesAt(string directoryPath)
    {
        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"Directory {directoryPath} does not exist");
        }

        return Directory.GetFiles(directoryPath);
    }

    public void MoveFile(string sourceFilePath, string destinationFilePath)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException($"Source file {sourceFilePath} does not exist");
        }

        var destinationParentDir = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destinationParentDir))
        {
            Directory.CreateDirectory(destinationParentDir);
        }

        File.Move(sourceFilePath, destinationFilePath);
        logger.LogInformation("Moved file from {SourceFilePath} to {DestinationFilePath}", sourceFilePath, destinationFilePath);
    }

    public long GetDirectorySize(string path)
    {
        return Directory
            .EnumerateFiles(path, "*", SearchOption.AllDirectories)
            .Sum(file => new FileInfo(file).Length);
    }
}
