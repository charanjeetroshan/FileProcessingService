using FileProcessingService.Infrastructure.FileStorage;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text;

namespace FileProcessingService.UnitTests;

public class LocalFileStorageServiceTests
{
    private string tempDirectory = null!;
    private LocalFileStorageService service = null!;

    [SetUp]
    public void Setup()
    {
        tempDirectory = Path.Combine(Path.GetTempPath(), $"local-file-storage-tests-{Guid.NewGuid()}");
        service = new LocalFileStorageService(NullLogger<LocalFileStorageService>.Instance);
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
    public async Task SaveAsync_WritesFileWithGuidPrefixedName()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("row1,row2"));

        var storedFileName = await service.SaveAsync("data.csv", tempDirectory, content, CancellationToken.None);

        Assert.That(storedFileName, Does.EndWith("_data.csv"));
        Assert.That(File.Exists(Path.Combine(tempDirectory, storedFileName)), Is.True);
    }

    [Test]
    public async Task SaveAsync_PersistsExactContent()
    {
        var text = "hello world";
        using var content = new MemoryStream(Encoding.UTF8.GetBytes(text));

        var storedFileName = await service.SaveAsync("greeting.txt", tempDirectory, content, CancellationToken.None);

        var savedText = await File.ReadAllTextAsync(Path.Combine(tempDirectory, storedFileName));
        Assert.That(savedText, Is.EqualTo(text));
    }
}
