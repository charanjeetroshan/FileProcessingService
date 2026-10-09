using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Domain.Constants;
using FileProcessingService.Infrastructure.FileStorage;
using FileProcessingService.Infrastructure.Imports;
using FileProcessingService.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FileProcessingService.UnitTests;

public class ImportJobProcessorTests
{
    private FileProcessingDbContext dbContext = null!;
    private SqliteConnection connection = null!;
    private string uploadDirectory = null!;
    private ICsvCustomerFileReader csvReaderMock = null!;
    private IValidator<CustomerImportRow> validatorMock = null!;

    [SetUp]
    public void Setup()
    {
        (dbContext, connection) = SqliteDbContextFactory.Create();
        uploadDirectory = Path.Combine(Path.GetTempPath(), $"import-job-processor-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(uploadDirectory);

        csvReaderMock = Substitute.For<ICsvCustomerFileReader>();
        validatorMock = Substitute.For<IValidator<CustomerImportRow>>();
    }

    [TearDown]
    public void TearDown()
    {
        dbContext.Dispose();
        connection.Dispose();

        if (Directory.Exists(uploadDirectory))
        {
            Directory.Delete(uploadDirectory, recursive: true);
        }
    }

    private ImportJobProcessor CreateProcessor()
    {
        var options = Options.Create(new FileStorageOptions { UploadDirectoryPath = uploadDirectory });
        var storage = new LocalFileStorageService(NullLogger<LocalFileStorageService>.Instance);
        return new ImportJobProcessor(options, storage, csvReaderMock, validatorMock, dbContext, NullLogger<ImportJobProcessor>.Instance);
    }

    private ImportJob CreateAndPersistJob(string fileName = FileSystemConstants.CustomerFileName)
    {
        var job = new ImportJob
        {
            UploadedFileName = "package.zip",
            DataPackageGuid = Guid.NewGuid().ToString(),
            CreatedAt = DateTimeOffset.UtcNow
        };
        var dataDirectory = Path.Combine(uploadDirectory, job.DataPackageGuid, FileSystemConstants.DataDirectoryName);
        Directory.CreateDirectory(dataDirectory);
        File.WriteAllText(Path.Combine(dataDirectory, fileName), string.Empty);

        dbContext.ImportJobs.Add(job);
        dbContext.SaveChanges();
        return job;
    }

    private static async IAsyncEnumerable<CustomerImportRow> ToAsyncEnumerable(IEnumerable<CustomerImportRow> rows)
    {
        foreach (var row in rows)
        {
            yield return row;
        }
        await Task.CompletedTask;
    }

    [TestCase("customers.csv")]
    [TestCase("CUSTOMERS.CSV")]
    public async Task ProcessJob_WithAllValidRows_CompletesSuccessfullyAndPersistsCustomers(string fileName)
    {
        var job = CreateAndPersistJob(fileName);
        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", DateOfBirth = "1990-01-01", Country = "US" }
        };

        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.ValidateAsync(Arg.Any<CustomerImportRow>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult());

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Completed));
        Assert.That(job.TotalRows, Is.EqualTo(1));
        Assert.That(job.SuccessfulRows, Is.EqualTo(1));
        Assert.That(job.FailedRows, Is.EqualTo(0));
        Assert.That(dbContext.Customers.Count(), Is.EqualTo(1));
        Assert.That(dbContext.Customers.Single().ImportId, Is.EqualTo(job.Id));
        Assert.That(job.ProcessedRows, Is.EqualTo(1));
        Assert.That(job.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task ProcessJob_WithInvalidRow_RecordsErrorAndSkipsRow()
    {
        var job = CreateAndPersistJob();
        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "", DateOfBirth = "1990-01-01", Country = "US" }
        };

        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.ValidateAsync(Arg.Any<CustomerImportRow>(), Arg.Any<CancellationToken>())
            .Returns(new ValidationResult([new ValidationFailure("Email", "Email is required") { ErrorCode = "Email.Required" }]));

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Completed));
        Assert.That(job.FailedRows, Is.EqualTo(1));
        Assert.That(job.SuccessfulRows, Is.EqualTo(0));
        Assert.That(dbContext.Customers.Count(), Is.EqualTo(0));
        Assert.That(dbContext.ImportErrors.Count(), Is.EqualTo(1));
        Assert.That(dbContext.ImportErrors.Single().ErrorCode, Is.EqualTo("Email.Required"));
    }

    [Test]
    public void ProcessJob_WhenCancelledDuringProcessing_MarksJobCancelledAndThrows()
    {
        var job = CreateAndPersistJob();
        using var cts = new CancellationTokenSource();

        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", DateOfBirth = "1990-01-01", Country = "US" }
        };

        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.ValidateAsync(Arg.Any<CustomerImportRow>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                cts.Cancel();
                cts.Token.ThrowIfCancellationRequested();
                return new ValidationResult();
            });

        var processor = CreateProcessor();

        Assert.ThrowsAsync<OperationCanceledException>(async () => await processor.ProcessJob(job, cts.Token));

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Cancelled));
        Assert.That(job.FailureReason, Is.Not.Null);
        Assert.That(job.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task ProcessJob_WhenReaderThrowsUnexpectedException_MarksJobFailed()
    {
        var job = CreateAndPersistJob();

        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("Corrupt file"));

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Failed));
        Assert.That(job.FailureReason, Is.EqualTo("Corrupt file"));
    }

    [Test]
    public async Task ProcessJob_SetsStartedAtAndProcessingStatusBeforeReadingRows()
    {
        var job = CreateAndPersistJob();

        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Assert.That(job.Status, Is.EqualTo(ImportStatus.Processing));
                Assert.That(job.StartedAt, Is.Not.Null);
                return ToAsyncEnumerable([]);
            });

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.StartedAt, Is.Not.Null);
    }

    [Test]
    public async Task ProcessJob_WithExtraFiles_ReadsOnlyExtractedCustomers()
    {
        var job = CreateAndPersistJob();
        var packageDirectory = Path.Combine(uploadDirectory, job.DataPackageGuid);
        var dataDirectory = Path.Combine(packageDirectory, FileSystemConstants.DataDirectoryName);
        File.WriteAllText(Path.Combine(dataDirectory, "orders.csv"), "ignored");
        var archiveDirectory = Path.Combine(packageDirectory, FileSystemConstants.ArchiveDirectoryName);
        Directory.CreateDirectory(archiveDirectory);
        File.WriteAllText(Path.Combine(archiveDirectory, "customers.csv"), "not extracted data");
        string? readPath = null;
        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            readPath = ((FileStream)call.Arg<Stream>()).Name;
            return ToAsyncEnumerable([]);
        });

        await CreateProcessor().ProcessJob(job, CancellationToken.None);

        Assert.That(readPath, Is.EqualTo(Path.Combine(dataDirectory, FileSystemConstants.CustomerFileName)));
        csvReaderMock.Received(1).ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>());
        Assert.That(job.Status, Is.EqualTo(ImportStatus.Completed));
        Assert.That(job.TotalRows, Is.Zero);
    }

    [TestCase(1000)]
    [TestCase(1001)]
    public async Task ProcessJob_WithBatchOfCustomers_PersistsEveryRow(int rowCount)
    {
        var job = CreateAndPersistJob();
        var rows = Enumerable.Range(1, rowCount).Select(i => new CustomerImportRow
        {
            RowNumber = i + 1,
            FirstName = " Jane ",
            LastName = " Doe ",
            Email = $"customer{i}@example.com",
            DateOfBirth = "1990-01-01",
            Country = " US "
        });
        csvReaderMock.ReadAsync(Arg.Any<Stream>(), Arg.Any<CancellationToken>()).Returns(ToAsyncEnumerable(rows));
        validatorMock.ValidateAsync(Arg.Any<CustomerImportRow>(), Arg.Any<CancellationToken>()).Returns(new ValidationResult());

        await CreateProcessor().ProcessJob(job, CancellationToken.None);

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Completed));
        Assert.That(job.ProcessedRows, Is.EqualTo(rowCount));
        Assert.That(job.SuccessfulRows, Is.EqualTo(rowCount));
        Assert.That(dbContext.Customers.Count(), Is.EqualTo(rowCount));
        Assert.That(dbContext.Customers.All(c => c.FirstName == "Jane" && c.LastName == "Doe" && c.Country == "US"), Is.True);
    }
}
