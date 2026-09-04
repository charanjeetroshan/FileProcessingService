using System.Text;
using FileProcessingService.Infrastructure.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace FileProcessingService.UnitTests;

public class LocalFileStorageServiceTests
{
    private string tempDirectory = null!;
    private LocalFileStorageService service = null!;

    [SetUp]
    public void Setup()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"local-file-storage-tests-{Guid.NewGuid()}");
        var options = Options.Create(new FileStorageOptions { UploadDirectoryPath = tempDirectory });
        service = new LocalFileStorageService(options, NullLogger<LocalFileStorageService>.Instance);
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
    public void Constructor_CreatesUploadDirectory()
    {
        Assert.That(Directory.Exists(tempDirectory), Is.True);
    }

    [Test]
    public async Task SaveAsync_WritesFileWithGuidPrefixedName()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("row1,row2"));

        var storedFileName = await service.SaveAsync("data.csv", content);

        Assert.That(storedFileName, Does.EndWith("_data.csv"));
        Assert.That(File.Exists(Path.Combine(tempDirectory, storedFileName)), Is.True);
    }

    [Test]
    public async Task SaveAsync_PersistsExactContent()
    {
        var text = "hello world";
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var storedFileName = await service.SaveAsync("greeting.txt", content);

        var savedText = await File.ReadAllTextAsync(Path.Combine(tempDirectory, storedFileName));
        Assert.That(savedText, Is.EqualTo(text));
    }

    [Test]
    public async Task OpenReadAsync_ReturnsReadableStreamForSavedFile()
    {
        var text = "readable content";
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var storedFileName = await service.SaveAsync("readable.txt", content);

        await using var readStream = await service.OpenReadAsync(storedFileName);
        using var reader = new StreamReader(readStream);
        var readText = await reader.ReadToEndAsync();

        Assert.That(readText, Is.EqualTo(text));
    }

    [Test]
    public void OpenReadAsync_WithMissingFile_ThrowsFileNotFoundException()
    {
        Assert.ThrowsAsync<FileNotFoundException>(async () => await service.OpenReadAsync("does-not-exist.csv"));
    }
}
