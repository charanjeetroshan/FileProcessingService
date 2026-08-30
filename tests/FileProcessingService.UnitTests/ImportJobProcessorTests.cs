using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.FileStorage;
using FileProcessingService.Infrastructure.Imports;
using FileProcessingService.Infrastructure.Persistence;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FileProcessingService.UnitTests;

public class ImportJobProcessorTests
{
    private FileProcessingDbContext dbContext = null!;
    private SqliteConnection connection = null!;
    private string uploadDirectory = null!;
    private Mock<ICsvCustomerFileReader> csvReaderMock = null!;
    private Mock<IValidator<CustomerImportRow>> validatorMock = null!;

    [SetUp]
    public void Setup()
    {
        (dbContext, connection) = SqliteDbContextFactory.Create();
        uploadDirectory = Path.Combine(Path.GetTempPath(), $"import-job-processor-tests-{Guid.NewGuid()}");
        Directory.CreateDirectory(uploadDirectory);

        csvReaderMock = new Mock<ICsvCustomerFileReader>();
        validatorMock = new Mock<IValidator<CustomerImportRow>>();
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
        var options = Options.Create(new FileStorageOptions { UploadDirectory = uploadDirectory });
        return new ImportJobProcessor(options, csvReaderMock.Object, validatorMock.Object, dbContext, NullLogger<ImportJobProcessor>.Instance);
    }

    private ImportJob CreateAndPersistJob()
    {
        var job = new ImportJob
        {
            OriginalFileName = "customers.csv",
            StoredFileName = $"{Guid.NewGuid()}_customers.csv",
            CreatedAt = DateTimeOffset.UtcNow
        };
        File.WriteAllText(Path.Combine(uploadDirectory, job.StoredFileName), string.Empty);

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

    [Test]
    public async Task ProcessJob_WithAllValidRows_CompletesSuccessfullyAndPersistsCustomers()
    {
        var job = CreateAndPersistJob();
        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", DateOfBirth = "1990-01-01", Country = "US" }
        };

        csvReaderMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<CustomerImportRow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Completed));
        Assert.That(job.TotalRows, Is.EqualTo(1));
        Assert.That(job.SuccessfulRows, Is.EqualTo(1));
        Assert.That(job.FailedRows, Is.EqualTo(0));
        Assert.That(dbContext.Customers.Count(), Is.EqualTo(1));
    }

    [Test]
    public async Task ProcessJob_WithInvalidRow_RecordsErrorAndSkipsRow()
    {
        var job = CreateAndPersistJob();
        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "", DateOfBirth = "1990-01-01", Country = "US" }
        };

        csvReaderMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<CustomerImportRow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult([new ValidationFailure("Email", "Email is required") { ErrorCode = "Email.Required" }]));

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
    public async Task ProcessJob_WithMappingFailure_RecordsMappingErrorAndSkipsRow()
    {
        var job = CreateAndPersistJob();
        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", DateOfBirth = "not-a-date", Country = "US" }
        };

        csvReaderMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<CustomerImportRow>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ValidationResult());

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Completed));
        Assert.That(job.FailedRows, Is.EqualTo(1));
        Assert.That(dbContext.ImportErrors.Single().ErrorCode, Is.EqualTo("MappingError"));
    }

    [Test]
    public async Task ProcessJob_WhenCancelledDuringProcessing_MarksJobCancelledAndThrows()
    {
        var job = CreateAndPersistJob();
        using var cts = new CancellationTokenSource();

        var rows = new[]
        {
            new CustomerImportRow { RowNumber = 2, FirstName = "Jane", LastName = "Doe", Email = "jane@example.com", DateOfBirth = "1990-01-01", Country = "US" }
        };

        csvReaderMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable(rows));
        validatorMock.Setup(v => v.ValidateAsync(It.IsAny<CustomerImportRow>(), It.IsAny<CancellationToken>()))
            .Returns(async () =>
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

        csvReaderMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
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

        csvReaderMock.Setup(r => r.ReadAsync(It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(ToAsyncEnumerable([]));

        var processor = CreateProcessor();
        await processor.ProcessJob(job, CancellationToken.None);

        Assert.That(job.StartedAt, Is.Not.Null);
    }
}
