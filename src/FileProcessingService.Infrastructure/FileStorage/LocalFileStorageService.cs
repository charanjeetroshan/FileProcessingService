using FileProcessingService.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileProcessingService.Infrastructure.FileStorage;

public class LocalFileStorageService(IOptions<FileStorageOptions> options, ILogger<LocalFileStorageService> logger) : IFileStorageService
{
    private readonly string root = EnsureDirectoryExists(options.Value.UploadDirectory);

    public async Task<string> SaveAsync(string fileName, Stream content, CancellationToken cancellationToken = default)
    {
        var storedFileName = $"{Guid.NewGuid()}_{fileName}";
        var fullPath = Path.Combine(root, storedFileName);

        await using var fileStream = File.Create(fullPath);
        await content.CopyToAsync(fileStream, cancellationToken);

        logger.LogInformation("Saved uploaded file {OriginalFileName} as {StoredFileName}", fileName, storedFileName);

        return storedFileName;
    }

    public Task<Stream> OpenReadAsync(string storedFileName, CancellationToken cancellationToken = default)
    {
        logger.LogDebug("Opening stored file {StoredFileName} for reading", storedFileName);
        Stream stream = File.OpenRead(Path.Combine(root, storedFileName));
        return Task.FromResult(stream);
    }

    private static string EnsureDirectoryExists(string path)
    {
        Directory.CreateDirectory(path);
        return path;
    }
}
