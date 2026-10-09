using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain.Constants;
using FileProcessingService.Domain.Entities;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FileProcessingService.UnitTests;

public class ImportJobEnqueuerServiceTests
{
    private IFileStorageService fileStorageServiceMock = null!;
    private IFileHasher fileHasherMock = null!;
    private IImportJobRepository importJobRepositoryMock = null!;
    private ImportJobEnqueuerService service = null!;
    private ICompressionService compressionMock = null!;
    private string uploadDirectory = null!;
    private string customerFilePath = null!;
    private string dataDirectory = null!;

    [SetUp]
    public void Setup()
    {
        fileStorageServiceMock = Substitute.For<IFileStorageService>();
        fileHasherMock = Substitute.For<IFileHasher>();
        importJobRepositoryMock = Substitute.For<IImportJobRepository>();

        uploadDirectory = Path.Combine(Path.GetTempPath(), $"import-enqueuer-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(uploadDirectory);
        customerFilePath = Path.Combine(uploadDirectory, "customers.csv");
        File.WriteAllText(customerFilePath, "customer data");
        dataDirectory = string.Empty;
        compressionMock = Substitute.For<ICompressionService>();
        compressionMock.DecompressAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dataDirectory = call.ArgAt<string>(1);
                return Task.CompletedTask;
            });
        fileStorageServiceMock.SaveAsync(Arg.Any<string>(), uploadDirectory, Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(Path.Combine(uploadDirectory, "package.zip"));
        fileStorageServiceMock.GetFilesAt(Arg.Any<string>()).Returns(_ => [customerFilePath]);
        fileStorageServiceMock.OpenFile(Arg.Any<string>()).Returns(call => File.OpenRead(call.Arg<string>()));
        fileStorageServiceMock.DirectoryExists(Arg.Any<string>()).Returns(true);
        fileHasherMock.ComputeHashAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns("file-hash");
        fileHasherMock.ComputeHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns("composite-hash");
        var options = Options.Create(new FileStorageOptions { UploadDirectoryPath = uploadDirectory });

        service = new ImportJobEnqueuerService(
            fileStorageServiceMock,
            fileHasherMock,
            importJobRepositoryMock,
            options,
            NullLogger<ImportJobEnqueuerService>.Instance,
            compressionMock);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(uploadDirectory))
        {
            Directory.Delete(uploadDirectory, recursive: true);
        }
    }

    private static IFormFile CreateFormFile(string fileName, string content = "content")
    {
        var formFile = Substitute.For<IFormFile>();
        formFile.FileName.Returns(fileName);
        formFile.OpenReadStream().Returns(_ => new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content)));
        return formFile;
    }

    [Test]
    public async Task EnqueueAsync_WithNoExistingJobForHash_SavesFileAndPersistsNewJob()
    {
        var file = CreateFormFile("package.zip");

        var result = await service.EnqueueAsync(file);

        Assert.That(result.IsDuplicate, Is.False);
        Assert.That(result.Job.UploadedFileName, Is.EqualTo("package.zip"));
        Assert.That(result.Job.DataPackageGuid, Is.Not.Empty);
        Assert.That(result.Job.CompositeHash, Is.EqualTo("composite-hash"));
        var packageDirectory = Path.Combine(uploadDirectory, result.Job.DataPackageGuid);
        Assert.That(dataDirectory, Is.EqualTo(Path.Combine(packageDirectory, FileSystemConstants.DataDirectoryName)));
        await compressionMock.Received(1).DecompressAsync(Path.Combine(uploadDirectory, "package.zip"), dataDirectory, Arg.Any<CancellationToken>());
        fileStorageServiceMock.Received(1).MoveFile(Path.Combine(uploadDirectory, "package.zip"), Path.Combine(packageDirectory, FileSystemConstants.ArchiveDirectoryName, "package.zip"));
        await fileHasherMock.Received(1).ComputeHashAsync("CUSTOMERS.CSV:file-hash|", Arg.Any<CancellationToken>());
        fileStorageServiceMock.DidNotReceive().DeleteDirectory(Arg.Any<string>());

        await importJobRepositoryMock.Received(1).AddAsync(result.Job, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnqueueAsync_WithExistingJobForHash_ReturnsDuplicateAndCleansNewPackage()
    {
        var file = CreateFormFile("package.zip");
        var existingJob = new ImportJob { UploadedFileName = "previous.zip", DataPackageGuid = "existing-package", CompositeHash = "composite-hash" };
        importJobRepositoryMock.GetByHashAsync("composite-hash", Arg.Any<CancellationToken>()).Returns(existingJob);

        var result = await service.EnqueueAsync(file);

        Assert.That(result.IsDuplicate, Is.True);
        Assert.That(result.Job, Is.SameAs(existingJob));

        await fileStorageServiceMock.Received(1).SaveAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        fileStorageServiceMock.Received(1).DeleteDirectory(Path.GetDirectoryName(dataDirectory)!);
        fileStorageServiceMock.DidNotReceive().DeleteDirectory(Path.Combine(uploadDirectory, existingJob.DataPackageGuid));
        await importJobRepositoryMock.DidNotReceive().AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [TestCase("customers.csv")]
    [TestCase("CUSTOMERS.CSV")]
    public async Task EnqueueAsync_WithSupportedAndUnsupportedFiles_HashesOnlyCustomers(string fileName)
    {
        var supportedPath = Path.Combine(uploadDirectory, fileName);
        File.WriteAllText(supportedPath, "customer data");
        var ignoredPath = Path.Combine(uploadDirectory, "notes.txt");
        fileStorageServiceMock.GetFilesAt(Arg.Any<string>()).Returns([ignoredPath, supportedPath]);

        var result = await service.EnqueueAsync(CreateFormFile("package.zip"));

        Assert.That(result.IsDuplicate, Is.False);
        fileStorageServiceMock.Received(1).OpenFile(supportedPath);
        fileStorageServiceMock.DidNotReceive().OpenFile(ignoredPath);
        await fileHasherMock.Received(1).ComputeHashAsync("CUSTOMERS.CSV:file-hash|", Arg.Any<CancellationToken>());
    }

    [TestCase("no-files", "contains no files")]
    [TestCase("unsupported", "contains no valid files")]
    [TestCase("too-many", "maximum allowed number of files")]
    [TestCase("too-large", "exceeds maximum allowed size")]
    [TestCase("empty", "contains only empty files")]
    public async Task EnqueueAsync_WithInvalidPackage_RejectsAndCleansPackage(string scenario, string message)
    {
        switch (scenario)
        {
            case "no-files":
                fileStorageServiceMock.GetFilesAt(Arg.Any<string>()).Returns(Array.Empty<string>());
                break;
            case "unsupported":
                fileStorageServiceMock.GetFilesAt(Arg.Any<string>()).Returns([Path.Combine(uploadDirectory, "orders.csv")]);
                break;
            case "too-many":
                fileStorageServiceMock.GetFilesAt(Arg.Any<string>()).Returns(Enumerable.Range(0, FileSystemConstants.MaxFileNumber + 1).Select(i => $"file{i}.csv").ToArray());
                break;
            case "too-large":
                fileStorageServiceMock.GetDirectorySize(Arg.Any<string>()).Returns(FileSystemConstants.MaxDirectorySizeBytes + 1024 * 1024);
                break;
            case "empty":
                File.WriteAllText(customerFilePath, string.Empty);
                break;
        }

        var exception = Assert.ThrowsAsync<ValidationException>(() => service.EnqueueAsync(CreateFormFile("package.zip")));

        Assert.That(exception!.Message, Does.Contain(message));
        fileStorageServiceMock.Received(1).DeleteDirectory(Path.GetDirectoryName(dataDirectory)!);
        await importJobRepositoryMock.DidNotReceive().GetByHashAsync(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await importJobRepositoryMock.DidNotReceive().AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnqueueAsync_AtExtractedSizeLimit_AcceptsPackage()
    {
        fileStorageServiceMock.GetDirectorySize(Arg.Any<string>()).Returns(FileSystemConstants.MaxDirectorySizeBytes);

        var result = await service.EnqueueAsync(CreateFormFile("package.zip"));

        Assert.That(result.IsDuplicate, Is.False);
        await importJobRepositoryMock.Received(1).AddAsync(result.Job, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task EnqueueAsync_WhenDecompressionFails_CleansPackageAndPropagatesException()
    {
        var failure = new InvalidDataException("Invalid ZIP archive");
        compressionMock.DecompressAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                dataDirectory = call.ArgAt<string>(1);
                return Task.FromException(failure);
            });

        var exception = Assert.ThrowsAsync<InvalidDataException>(() => service.EnqueueAsync(CreateFormFile("package.zip")));

        Assert.That(exception, Is.SameAs(failure));
        fileStorageServiceMock.Received(1).DeleteDirectory(Path.GetDirectoryName(dataDirectory)!);
        fileStorageServiceMock.DidNotReceive().MoveFile(Arg.Any<string>(), Arg.Any<string>());
        await importJobRepositoryMock.DidNotReceive().AddAsync(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }
}
