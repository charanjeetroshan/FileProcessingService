using FileProcessingService.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace FileProcessingService.Infrastructure.FileStorage;

public class LocalFileStorageService(ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    public async Task<string> SaveAsync(string fileName, string rootDirectoryPath, Stream content, CancellationToken cancellationToken)
    {
        // Ensure the root directory exists
        Directory.CreateDirectory(rootDirectoryPath);

        var storedFileName = $"{Guid.NewGuid()}_{fileName}";
        var fullPath = Path.Combine(rootDirectoryPath, storedFileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        logger.LogInformation("Saved uploaded file {OriginalFileName} as {StoredFileName}", fileName, storedFileName);

        return storedFileName;
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

    public string[] GetFilesAt(string directoryPath)
    {
        if (Directory.Exists(directoryPath))
        {
            return Directory.GetFiles(directoryPath);
        }

        return [];
    }
}
