using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Imports;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileProcessingService.UnitTests;

public class ImportErrorRepositoryTests
{
    private FileProcessingDbContext dbContext = null!;
    private SqliteConnection connection = null!;
    private ImportErrorRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        (dbContext, connection) = SqliteDbContextFactory.Create();
        repository = new ImportErrorRepository(dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    private static ImportError CreateError(
        Guid importJobId,
        long rowNumber,
        string? field = "Email",
        string errorCode = "EmptyValue") => new()
    {
        ImportJobId = importJobId,
        RowNumber = rowNumber,
        Field = field,
        ErrorCode = errorCode,
        ErrorMessage = "Some error message",
        RawValue = "raw"
    };

    private async Task AddErrorsAsync(params ImportError[] errors)
    {
        await dbContext.ImportErrors.AddRangeAsync(errors);
        await dbContext.SaveChangesAsync();
    }

    [Test]
    public async Task GetByImportJobIdAsync_ReturnsOnlyErrorsForGivenJob()
    {
        var jobId = Guid.NewGuid();
        var otherJobId = Guid.NewGuid();
        await AddErrorsAsync(
            CreateError(jobId, 1),
            CreateError(jobId, 2),
            CreateError(otherJobId, 1));

        var (items, totalCount) = await repository.GetByImportJobIdAsync(jobId, page: 1, pageSize: 20);

        Assert.That(totalCount, Is.EqualTo(2));
        Assert.That(items, Has.Count.EqualTo(2));
        Assert.That(items.All(e => e.ImportJobId == jobId), Is.True);
    }

    [Test]
    public async Task GetByImportJobIdAsync_OrdersByRowNumberAscending()
    {
        var jobId = Guid.NewGuid();
        await AddErrorsAsync(
            CreateError(jobId, 5),
            CreateError(jobId, 1),
            CreateError(jobId, 3));

        var (items, _) = await repository.GetByImportJobIdAsync(jobId, page: 1, pageSize: 20);

        Assert.That(items.Select(e => e.RowNumber), Is.EqualTo(new long[] { 1, 3, 5 }));
    }

    [Test]
    public async Task GetByImportJobIdAsync_PaginatesResults()
    {
        var jobId = Guid.NewGuid();
        await AddErrorsAsync(
            CreateError(jobId, 1),
            CreateError(jobId, 2),
            CreateError(jobId, 3),
            CreateError(jobId, 4),
            CreateError(jobId, 5));

        var (firstPage, totalCount) = await repository.GetByImportJobIdAsync(jobId, page: 1, pageSize: 2);
        var (secondPage, _) = await repository.GetByImportJobIdAsync(jobId, page: 2, pageSize: 2);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(firstPage.Select(e => e.RowNumber), Is.EqualTo(new long[] { 1, 2 }));
        Assert.That(secondPage.Select(e => e.RowNumber), Is.EqualTo(new long[] { 3, 4 }));
    }

    [Test]
    public async Task GetByImportJobIdAsync_FiltersByField()
    {
        var jobId = Guid.NewGuid();
        await AddErrorsAsync(
            CreateError(jobId, 1, field: "Email"),
            CreateError(jobId, 2, field: "Country"));

        var (items, totalCount) = await repository.GetByImportJobIdAsync(jobId, page: 1, pageSize: 20, field: "Country");

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Field, Is.EqualTo("Country"));
    }

    [Test]
    public async Task GetByImportJobIdAsync_FiltersByErrorCode()
    {
        var jobId = Guid.NewGuid();
        await AddErrorsAsync(
            CreateError(jobId, 1, errorCode: "EmptyValue"),
            CreateError(jobId, 2, errorCode: "InvalidDate"));

        var (items, totalCount) = await repository.GetByImportJobIdAsync(jobId, page: 1, pageSize: 20, errorCode: "InvalidDate");

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].ErrorCode, Is.EqualTo("InvalidDate"));
    }

    [Test]
    public async Task GetByImportJobIdAsync_WithNoMatchingErrors_ReturnsEmpty()
    {
        var (items, totalCount) = await repository.GetByImportJobIdAsync(Guid.NewGuid(), page: 1, pageSize: 20);

        Assert.That(totalCount, Is.EqualTo(0));
        Assert.That(items, Is.Empty);
    }
}
