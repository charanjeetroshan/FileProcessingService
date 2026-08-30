using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.Imports;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileProcessingService.UnitTests;

public class ImportJobRepositoryTests
{
    private FileProcessingDbContext dbContext = null!;
    private SqliteConnection connection = null!;
    private ImportJobRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        (dbContext, connection) = SqliteDbContextFactory.Create();
        repository = new ImportJobRepository(dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    private static ImportJob CreateJob(ImportStatus status = ImportStatus.Pending, DateTimeOffset? createdAt = null, string? fileHash = null) => new()
    {
        OriginalFileName = "file.csv",
        StoredFileName = $"{Guid.NewGuid()}_file.csv",
        Status = status,
        CreatedAt = createdAt ?? DateTimeOffset.UtcNow,
        FileHash = fileHash
    };

    [Test]
    public async Task AddAsync_PersistsJob()
    {
        var job = CreateJob();

        await repository.AddAsync(job);

        var stored = await dbContext.ImportJobs.FindAsync(job.Id);
        Assert.That(stored, Is.Not.Null);
    }

    [Test]
    public async Task GetByIdAsync_WithExistingId_ReturnsJob()
    {
        var job = CreateJob();
        await repository.AddAsync(job);

        var found = await repository.GetByIdAsync(job.Id);

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(job.Id));
    }

    [Test]
    public async Task GetByIdAsync_WithNonExistentId_ReturnsNull()
    {
        var found = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task ClaimNextPendingJobAsync_ClaimsOldestPendingJobAndFlipsStatus()
    {
        var job = CreateJob();
        await repository.AddAsync(job);

        var claimed = await repository.ClaimNextPendingJobAsync();

        Assert.That(claimed, Is.Not.Null);
        Assert.That(claimed!.Id, Is.EqualTo(job.Id));
        Assert.That(claimed.Status, Is.EqualTo(ImportStatus.Processing));
    }

    [Test]
    public async Task ClaimNextPendingJobAsync_WithNoPendingJobs_ReturnsNull()
    {
        var claimed = await repository.ClaimNextPendingJobAsync();

        Assert.That(claimed, Is.Null);
    }

    [Test]
    public async Task ClaimNextPendingJobAsync_DoesNotReclaimAlreadyProcessingJob()
    {
        var job = CreateJob();
        await repository.AddAsync(job);

        var firstClaim = await repository.ClaimNextPendingJobAsync();
        var secondClaim = await repository.ClaimNextPendingJobAsync();

        Assert.That(firstClaim, Is.Not.Null);
        Assert.That(secondClaim, Is.Null);
    }

    [Test]
    public async Task GetByFileHashAsync_WithMatchingActiveJob_ReturnsJob()
    {
        var job = CreateJob(fileHash: "abc123");
        await repository.AddAsync(job);

        var found = await repository.GetByFileHashAsync("abc123");

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(job.Id));
    }

    [Test]
    public async Task GetByFileHashAsync_WithOnlyFailedMatchingJob_ReturnsNull()
    {
        var job = CreateJob(status: ImportStatus.Failed, fileHash: "abc123");
        await repository.AddAsync(job);

        var found = await repository.GetByFileHashAsync("abc123");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task GetByFileHashAsync_WithOnlyCancelledMatchingJob_ReturnsNull()
    {
        var job = CreateJob(status: ImportStatus.Cancelled, fileHash: "abc123");
        await repository.AddAsync(job);

        var found = await repository.GetByFileHashAsync("abc123");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task GetByFileHashAsync_WithNoMatchingHash_ReturnsNull()
    {
        var job = CreateJob(fileHash: "abc123");
        await repository.AddAsync(job);

        var found = await repository.GetByFileHashAsync("different-hash");

        Assert.That(found, Is.Null);
    }

    [Test]
    public async Task GetPagedAsync_ReturnsNewestFirst()
    {
        var older = CreateJob(createdAt: DateTimeOffset.UtcNow.AddMinutes(-10));
        var newer = CreateJob(createdAt: DateTimeOffset.UtcNow);
        await repository.AddAsync(older);
        await repository.AddAsync(newer);

        var (items, totalCount) = await repository.GetPagedAsync(page: 1, pageSize: 20);

        Assert.That(totalCount, Is.EqualTo(2));
        Assert.That(items.Select(j => j.Id), Is.EqualTo(new[] { newer.Id, older.Id }));
    }

    [Test]
    public async Task GetPagedAsync_PaginatesResults()
    {
        for (var i = 0; i < 5; i++)
        {
            await repository.AddAsync(CreateJob(createdAt: DateTimeOffset.UtcNow.AddMinutes(-i)));
        }

        var (firstPage, totalCount) = await repository.GetPagedAsync(page: 1, pageSize: 2);
        var (secondPage, _) = await repository.GetPagedAsync(page: 2, pageSize: 2);

        Assert.That(totalCount, Is.EqualTo(5));
        Assert.That(firstPage, Has.Count.EqualTo(2));
        Assert.That(secondPage, Has.Count.EqualTo(2));
        Assert.That(firstPage.Select(j => j.Id), Is.Not.EquivalentTo(secondPage.Select(j => j.Id)));
    }

    [Test]
    public async Task GetPagedAsync_FiltersByStatus()
    {
        var pending = CreateJob(status: ImportStatus.Pending);
        var completed = CreateJob(status: ImportStatus.Completed);
        await repository.AddAsync(pending);
        await repository.AddAsync(completed);

        var (items, totalCount) = await repository.GetPagedAsync(page: 1, pageSize: 20, status: ImportStatus.Completed);

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Id, Is.EqualTo(completed.Id));
    }

    [Test]
    public async Task GetPagedAsync_FiltersByFileNameSubstring()
    {
        var job1 = CreateJob();
        job1.OriginalFileName = "customers-january.csv";
        var job2 = CreateJob();
        job2.OriginalFileName = "customers-february.csv";
        await repository.AddAsync(job1);
        await repository.AddAsync(job2);

        var (items, totalCount) = await repository.GetPagedAsync(page: 1, pageSize: 20, fileName: "january");

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Id, Is.EqualTo(job1.Id));
    }

    [Test]
    public async Task GetPagedAsync_FiltersByCreatedDateRange()
    {
        var now = DateTimeOffset.UtcNow;
        var tooOld = CreateJob(createdAt: now.AddDays(-10));
        var inRange = CreateJob(createdAt: now.AddDays(-2));
        var tooNew = CreateJob(createdAt: now.AddDays(1));
        await repository.AddAsync(tooOld);
        await repository.AddAsync(inRange);
        await repository.AddAsync(tooNew);

        var (items, totalCount) = await repository.GetPagedAsync(
            page: 1, pageSize: 20,
            createdFrom: now.AddDays(-5),
            createdTo: now.AddDays(-1));

        Assert.That(totalCount, Is.EqualTo(1));
        Assert.That(items, Has.Count.EqualTo(1));
        Assert.That(items[0].Id, Is.EqualTo(inRange.Id));
    }

    [Test]
    public async Task GetPagedAsync_WithNoJobs_ReturnsEmpty()
    {
        var (items, totalCount) = await repository.GetPagedAsync(page: 1, pageSize: 20);

        Assert.That(totalCount, Is.EqualTo(0));
        Assert.That(items, Is.Empty);
    }
}
