using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FileProcessingService.UnitTests;

public class ImportUploadServiceTests
{
    private IFileStorageService fileStorageServiceMock = null!;
    private IFileHasher fileHasherMock = null!;
    private IImportJobRepository importJobRepositoryMock = null!;
    private ImportUploadService service = null!;

    [SetUp]
    public void Setup()
    {
        fileStorageServiceMock = Substitute.For<IFileStorageService>();
        fileHasherMock = Substitute.For<IFileHasher>();
        importJobRepositoryMock = Substitute.For<IImportJobRepository>();

        var options = Options.Create(new FileStorageOptions { UploadDirectoryPath = "uploads" });

        service = new ImportUploadService(
            fileStorageServiceMock,
            fileHasherMock,
            importJobRepositoryMock,
            options,
            NullLogger<ImportUploadService>.Instance);
    }

    private static IFormFile CreateFormFile(string fileName, string content = "content")
    {
        var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns(fileName);
        formFile.OpenReadStream().Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
        return formFile;
    }

    [Test]
    public async Task UploadAsync_WithNoExistingJobForHash_SavesFileAndPersistsNewJob()
    {
        var file = CreateFormFile("customers.csv");
        fileHasherMock.ComputeHashAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("hash-123");
        importJobRepositoryMock.GetByFileHashAsync("hash-123", Arg.Any<CancellationToken>()).Returns((ImportJob?)null);
        fileStorageServiceMock.SaveAsync("customers.csv", "uploads", Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns("stored-customers.csv");

        var result = await service.UploadAsync(file);

        Assert.That(result.IsDuplicate, Is.False);
        Assert.That(result.Job.OriginalFileName, Is.EqualTo("customers.csv"));
        Assert.That(result.Job.StoredFileName, Is.EqualTo("stored-customers.csv"));
        Assert.That(result.Job.FileHash, Is.EqualTo("hash-123"));

        await importJobRepositoryMock.Received(1).AddAsync(result.Job, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task UploadAsync_WithExistingJobForHash_ReturnsDuplicateWithoutSavingFile()
    {
        var file = CreateFormFile("customers.csv");
        var existingJob = new ImportJob { OriginalFileName = "customers.csv", StoredFileName = "old-stored.csv", FileHash = "hash-123" };

        fileHasherMock.ComputeHashAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("hash-123");
        importJobRepositoryMock.GetByFileHashAsync("hash-123", Arg.Any<CancellationToken>()).Returns(existingJob);

        var result = await service.UploadAsync(file);

        Assert.That(result.IsDuplicate, Is.True);
        Assert.That(result.Job, Is.SameAs(existingJob));

        await fileStorageServiceMock.DidNotReceive().SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        await importJobRepositoryMock.DidNotReceive().AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }
}
