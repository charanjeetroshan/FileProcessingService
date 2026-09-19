using FileProcessingService.Infrastructure.FileStorage;
using System.IO.Compression;

namespace FileProcessingService.UnitTests;

public class CompressorTests
{
    private readonly Compressor compressor = new();
    private readonly string tempDirectory = Path.Combine(Path.GetTempPath(), $"compressor-tests-{Guid.NewGuid()}");

    [SetUp]
    public void SetUp()
    {
        Directory.CreateDirectory(tempDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(tempDirectory))
        {
            Directory.Delete(tempDirectory, recursive: true);
        }
    }

    [Test]
    public async Task Decompress_UnzipsArchiveCorrectly()
    {
        string sourceDir = Path.Combine(tempDirectory, Guid.NewGuid().ToString());
        Directory.CreateDirectory(sourceDir);
        await CreateFiles(sourceDir);

        string destFileName = sourceDir + ".zip";
        await ZipFile.CreateFromDirectoryAsync(sourceDir, destFileName);
        Directory.Delete(sourceDir, recursive: true);

        Directory.CreateDirectory(sourceDir);

        await compressor.DecompressAsync(destFileName, sourceDir, CancellationToken.None);
        string[] decompressedFilePaths = Directory.GetFiles(sourceDir);

        Assert.True(Directory.Exists(sourceDir));
        Assert.That(decompressedFilePaths, Has.Length.EqualTo(5));

        for (int i = 0; i < decompressedFilePaths.Length; i++)
        {
            var filePath = decompressedFilePaths[i];

            Assert.True(File.Exists(filePath));

            string content = await File.ReadAllTextAsync(filePath);
            Assert.That(content, Contains.Substring($"This is file no."));
        }
    }

    [Test]
    public void Decompress_ThrowsFileNotFoundException_WhenSourceFileDoesNotExist()
    {
        string sourceZip = Path.Combine(tempDirectory, "does-not-exist.zip");

        Assert.ThrowsAsync<FileNotFoundException>(
            () => compressor.DecompressAsync(sourceZip, tempDirectory, CancellationToken.None));
    }

    [Test]
    public async Task Decompress_ThrowsInvalidOperationException_WhenSourceFileIsNotZip()
    {
        string notAZip = Path.Combine(tempDirectory, "archive.rar");
        await File.WriteAllTextAsync(notAZip, "not a real archive");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => compressor.DecompressAsync(notAZip, tempDirectory, CancellationToken.None));
    }

    [Test]
    public void Decompress_ThrowsDirectoryNotFoundException_WhenDestinationDirectoryDoesNotExist()
    {
        string sourceDir = Path.Combine(tempDirectory, "source");
        Directory.CreateDirectory(sourceDir);

        string destFileName = sourceDir + ".zip";
        ZipFile.CreateFromDirectory(sourceDir, destFileName);

        string missingDestination = Path.Combine(tempDirectory, "missing-destination");

        Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => compressor.DecompressAsync(destFileName, missingDestination, CancellationToken.None));
    }

    [Test]
    public async Task Decompress_PreservesExistingDestinationContent_WhenExtractionFails()
    {
        string destinationDir = Path.Combine(tempDirectory, "destination");
        Directory.CreateDirectory(destinationDir);

        string preExistingFile = Path.Combine(destinationDir, "pre-existing.txt");
        await File.WriteAllTextAsync(preExistingFile, "keep me");

        string corruptZip = Path.Combine(tempDirectory, "corrupt.zip");
        await File.WriteAllTextAsync(corruptZip, "this is not a valid zip archive");

        Assert.ThrowsAsync<InvalidDataException>(
            () => compressor.DecompressAsync(corruptZip, destinationDir, CancellationToken.None));

        Assert.True(File.Exists(preExistingFile));
        Assert.That(await File.ReadAllTextAsync(preExistingFile), Is.EqualTo("keep me"));
    }

    [Test]
    public async Task Compress_CreatesArchiveContainingAllSourceFiles()
    {
        string sourceDir = Path.Combine(tempDirectory, "source");
        Directory.CreateDirectory(sourceDir);
        await CreateFiles(sourceDir);

        string destFileName = Path.Combine(tempDirectory, "archive.zip");

        await compressor.CompressAsync(sourceDir, destFileName, CancellationToken.None);

        Assert.True(File.Exists(destFileName));

        using ZipArchive archive = ZipFile.OpenRead(destFileName);
        Assert.That(archive.Entries, Has.Count.EqualTo(5));
    }

    [Test]
    public void Compress_ThrowsDirectoryNotFoundException_WhenSourceDirectoryDoesNotExist()
    {
        string missingSource = Path.Combine(tempDirectory, "missing-source");
        string destFileName = Path.Combine(tempDirectory, "archive.zip");

        Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => compressor.CompressAsync(missingSource, destFileName, CancellationToken.None));
    }

    [Test]
    public async Task Compress_ThrowsInvalidOperationException_WhenDestinationExtensionIsNotZip()
    {
        string sourceDir = Path.Combine(tempDirectory, "source");
        Directory.CreateDirectory(sourceDir);
        await CreateFiles(sourceDir);

        string destFileName = Path.Combine(tempDirectory, "archive.rar");

        Assert.ThrowsAsync<InvalidOperationException>(
            () => compressor.CompressAsync(sourceDir, destFileName, CancellationToken.None));
    }

    [Test]
    public void Compress_ThrowsDirectoryNotFoundException_WhenDestinationDirectoryDoesNotExist()
    {
        string sourceDir = Path.Combine(tempDirectory, "source");
        Directory.CreateDirectory(sourceDir);

        string destFileName = Path.Combine(tempDirectory, "missing-folder", "archive.zip");

        Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => compressor.CompressAsync(sourceDir, destFileName, CancellationToken.None));
    }

    [Test]
    public async Task Compress_OverwritesExistingDestinationFile_WithoutLeftoverBytes()
    {
        string sourceDir = Path.Combine(tempDirectory, "source");
        Directory.CreateDirectory(sourceDir);
        await CreateFiles(sourceDir, count: 1);

        string destFileName = Path.Combine(tempDirectory, "archive.zip");

        // Pre-create a larger "stale" file at the destination path.
        await File.WriteAllBytesAsync(destFileName, new byte[1024 * 10]);

        await compressor.CompressAsync(sourceDir, destFileName, CancellationToken.None);

        using ZipArchive archive = ZipFile.OpenRead(destFileName);
        Assert.That(archive.Entries, Has.Count.EqualTo(1));
    }

    private static async Task CreateFiles(string destinationDirectoryPath, short count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            var filePath = Path.Combine(destinationDirectoryPath, Path.GetRandomFileName() + ".txt");
            await File.WriteAllTextAsync(filePath, $"This is file no. {i}.");
        }
    }
}
