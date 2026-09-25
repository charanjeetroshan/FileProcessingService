using FileProcessingService.Application.Abstractions;
using System.IO.Compression;

namespace FileProcessingService.Infrastructure.FileStorage;

internal sealed class CompressionService : ICompressionService
{
    public async Task CompressAsync(string sourceDirectory, string destinationFilePath, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(sourceDirectory))
        {
            throw new DirectoryNotFoundException($"Requested directory to archive {sourceDirectory} was not found");
        }

        if (!string.Equals(Path.GetExtension(destinationFilePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid archive file extension. Only zip is valid/allowed.");
        }

        string? destinationDirectory = Path.GetDirectoryName(destinationFilePath);
        if (!string.IsNullOrEmpty(destinationDirectory) && !Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException($"Destination directory {destinationDirectory} was not found");
        }

        try
        {
            await using FileStream stream = new(destinationFilePath, FileMode.Create);
            await ZipFile.CreateFromDirectoryAsync(sourceDirectory, stream, CompressionLevel.Optimal, includeBaseDirectory: false, cancellationToken);
        }
        catch
        {
            if (File.Exists(destinationFilePath))
            {
                File.Delete(destinationFilePath);
            }

            throw;
        }
    }

    public async Task DecompressAsync(string sourceFilePath, string destinationDirectory, CancellationToken cancellationToken)
    {
        if (!File.Exists(sourceFilePath))
        {
            throw new FileNotFoundException("Requested file was not found", sourceFilePath);
        }

        if (!string.Equals(Path.GetExtension(sourceFilePath), ".zip", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Invalid archive file extension. Only zip is valid/allowed.");
        }

        if (!Directory.Exists(destinationDirectory))
        {
            throw new DirectoryNotFoundException($"Specified destination directory {destinationDirectory} was not found");
        }

        string stagingDirectory = Path.Combine(Path.GetTempPath(), $"decompress-staging-{Guid.NewGuid()}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            await using (FileStream stream = File.OpenRead(sourceFilePath))
            {
                await ZipFile.ExtractToDirectoryAsync(stream, stagingDirectory, overwriteFiles: true, cancellationToken);
            }

            foreach (string sourcePath in Directory.EnumerateFileSystemEntries(stagingDirectory, "*", SearchOption.AllDirectories))
            {
                string relativePath = Path.GetRelativePath(stagingDirectory, sourcePath);
                string targetPath = Path.Combine(destinationDirectory, relativePath);

                if (Directory.Exists(sourcePath))
                {
                    Directory.CreateDirectory(targetPath);
                }
                else
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                    File.Move(sourcePath, targetPath, overwrite: true);
                }
            }
        }
        finally
        {
            Directory.Delete(stagingDirectory, recursive: true);
        }
    }
}
