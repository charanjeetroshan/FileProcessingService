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
    public async Task SaveAsync_WritesRequestedFileAndReturnsFullPath()
    {
        using var content = new MemoryStream(Encoding.UTF8.GetBytes("row1,row2"));

        var storedFileName = await service.SaveAsync("data.csv", tempDirectory, content, CancellationToken.None);

        Assert.That(storedFileName, Is.EqualTo(Path.Combine(tempDirectory, "data.csv")));
        Assert.That(File.Exists(storedFileName), Is.True);
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

    [Test]
    public async Task DeleteFile_WithExistingFile_DeletesFile()
    {
        Directory.CreateDirectory(tempDirectory);
        var filePath = Path.Combine(tempDirectory, "to-delete.txt");
        await File.WriteAllTextAsync(filePath, "content");

        service.DeleteFile(filePath);

        Assert.That(File.Exists(filePath), Is.False);
    }

    [Test]
    public void DeleteFile_WithMissingFile_ThrowsFileProcessingException()
    {
        var filePath = Path.Combine(tempDirectory, "missing.txt");

        Assert.Throws<FileNotFoundException>(() => service.DeleteFile(filePath));
    }

    [Test]
    public async Task GetFilesAt_WithExistingDirectory_ReturnsFiles()
    {
        Directory.CreateDirectory(tempDirectory);
        var filePath = Path.Combine(tempDirectory, "file1.txt");
        await File.WriteAllTextAsync(filePath, "content");

        var files = service.GetFilesAt(tempDirectory);

        Assert.That(files, Has.Length.EqualTo(1));
        Assert.That(files[0], Is.EqualTo(filePath));
    }

    [Test]
    public void GetFilesAt_WithMissingDirectory_ThrowsDirectoryNotFoundException()
    {
        Assert.Throws<DirectoryNotFoundException>(() => service.GetFilesAt(tempDirectory));
    }

    [Test]
    public void GetFilesAt_ReturnsOnlyTopLevelFiles()
    {
        var nested = Path.Combine(tempDirectory, "nested");
        Directory.CreateDirectory(nested);
        var customerPath = Path.Combine(tempDirectory, "customers.csv");
        File.WriteAllText(customerPath, "customers");
        File.WriteAllText(Path.Combine(nested, "notes.txt"), "notes");

        Assert.That(service.GetFilesAt(tempDirectory), Is.EqualTo(new[] { customerPath }));
    }

    [Test]
    public void GetDirectorySize_IncludesNestedFiles()
    {
        var nested = Path.Combine(tempDirectory, "nested");
        Directory.CreateDirectory(nested);
        File.WriteAllBytes(Path.Combine(tempDirectory, "customers.csv"), new byte[10]);
        File.WriteAllBytes(Path.Combine(nested, "notes.txt"), new byte[20]);

        Assert.That(service.GetDirectorySize(tempDirectory), Is.EqualTo(30));
    }

    [Test]
    public void GetDirectorySize_WithEmptyDirectory_ReturnsZero()
    {
        Directory.CreateDirectory(tempDirectory);

        Assert.That(service.GetDirectorySize(tempDirectory), Is.Zero);
    }

    [Test]
    public void DeleteDirectory_RemovesNestedFilesAndDirectory()
    {
        var nested = Path.Combine(tempDirectory, "package", "data");
        Directory.CreateDirectory(nested);
        File.WriteAllText(Path.Combine(nested, "customers.csv"), "data");
        Assert.That(service.DirectoryExists(tempDirectory), Is.True);

        service.DeleteDirectory(tempDirectory);

        Assert.That(service.DirectoryExists(tempDirectory), Is.False);
    }

    [Test]
    public void DeleteDirectory_WithMissingDirectory_Throws()
    {
        Assert.Throws<DirectoryNotFoundException>(() => service.DeleteDirectory(tempDirectory));
    }

    [Test]
    public async Task MoveFile_CreatesDestinationParentAndPreservesContent()
    {
        Directory.CreateDirectory(tempDirectory);
        var source = Path.Combine(tempDirectory, "package.zip");
        var destination = Path.Combine(tempDirectory, "package", "archive", "package.zip");
        await File.WriteAllTextAsync(source, "archive bytes");

        service.MoveFile(source, destination);

        Assert.That(File.Exists(source), Is.False);
        Assert.That(await File.ReadAllTextAsync(destination), Is.EqualTo("archive bytes"));
    }

    [Test]
    public void MoveFile_WithMissingSource_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => service.MoveFile(Path.Combine(tempDirectory, "missing.zip"), Path.Combine(tempDirectory, "archive", "missing.zip")));
    }

    [Test]
    public void OpenFile_WithMissingFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => service.OpenFile(Path.Combine(tempDirectory, "missing.csv")));
    }
}
