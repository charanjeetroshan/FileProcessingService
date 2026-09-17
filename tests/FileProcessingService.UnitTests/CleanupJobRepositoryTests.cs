using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.Cleanup;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileProcessingService.UnitTests;

public class CleanupJobRepositoryTests
{
    private FileProcessingDbContext dbContext = null!;
    private SqliteConnection connection = null!;
    private CleanupJobRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        (dbContext, connection) = SqliteDbContextFactory.Create();
        repository = new CleanupJobRepository(dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    private static CleanupJob CreateJob(string filePath, CleanupStatus status = CleanupStatus.Pending) => new()
    {
        FilePath = filePath,
        Status = status,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Test]
    public async Task AddJobsAsync_PersistsJobs()
    {
        var job = CreateJob("C:/temp/file.csv");

        await repository.AddJobsAsync([job]);
        await repository.SaveChangesAsync();

        var stored = await dbContext.CleanupJobs.FindAsync(job.Id);
        Assert.That(stored, Is.Not.Null);
    }

    [Test]
    public async Task ClaimByCountAsync_ClaimsPendingJobsAndFlipsStatusToCleaningUp()
    {
        var job = CreateJob("C:/temp/file.csv");
        await repository.AddJobsAsync([job]);
        await repository.SaveChangesAsync();

        var claimed = await repository.ClaimByCountAsync();

        Assert.That(claimed, Has.Length.EqualTo(1));
        Assert.That(claimed[0].Status, Is.EqualTo(CleanupStatus.CleaningUp));
    }

    [Test]
    public async Task ClaimByCountAsync_RespectsCountLimit()
    {
        var jobs = Enumerable.Range(0, 5).Select(i => CreateJob($"C:/temp/file{i}.csv")).ToArray();
        await repository.AddJobsAsync(jobs);
        await repository.SaveChangesAsync();

        var claimed = await repository.ClaimByCountAsync(count: 2);

        Assert.That(claimed, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task ClaimByCountAsync_DoesNotReclaimAlreadyClaimedJobs()
    {
        var job = CreateJob("C:/temp/file.csv");
        await repository.AddJobsAsync([job]);
        await repository.SaveChangesAsync();

        var firstClaim = await repository.ClaimByCountAsync();
        var secondClaim = await repository.ClaimByCountAsync();

        Assert.That(firstClaim, Has.Length.EqualTo(1));
        Assert.That(secondClaim, Is.Empty);
    }

    [Test]
    public async Task ClaimByCountAsync_WithNoPendingJobs_ReturnsEmpty()
    {
        var claimed = await repository.ClaimByCountAsync();

        Assert.That(claimed, Is.Empty);
    }

    [Test]
    public async Task GetExistingCleanupFilePathsAsync_ReturnsPathsForNonDoneJobs()
    {
        var pending = CreateJob("C:/temp/pending.csv", CleanupStatus.Pending);
        var cleaningUp = CreateJob("C:/temp/cleaning.csv", CleanupStatus.CleaningUp);
        var failed = CreateJob("C:/temp/failed.csv", CleanupStatus.Failed);
        await repository.AddJobsAsync([pending, cleaningUp, failed]);
        await repository.SaveChangesAsync();

        var existingPaths = await repository.GetExistingCleanupFilePathsAsync();

        Assert.That(existingPaths, Is.EquivalentTo(new[] { pending.FilePath, cleaningUp.FilePath, failed.FilePath }));
    }

    [Test]
    public async Task GetExistingCleanupFilePathsAsync_ExcludesDoneJobs()
    {
        var done = CreateJob("C:/temp/done.csv", CleanupStatus.Done);
        await repository.AddJobsAsync([done]);
        await repository.SaveChangesAsync();

        var existingPaths = await repository.GetExistingCleanupFilePathsAsync();

        Assert.That(existingPaths, Is.Empty);
    }

    [Test]
    public async Task SaveChangesAsync_PersistsStatusChanges()
    {
        var job = CreateJob("C:/temp/file.csv");
        await repository.AddJobsAsync([job]);
        await repository.SaveChangesAsync();

        var claimed = await repository.ClaimByCountAsync();
        claimed[0].Status = CleanupStatus.Done;
        claimed[0].CompletedAt = DateTimeOffset.UtcNow;
        await repository.SaveChangesAsync();

        var stored = await dbContext.CleanupJobs.FindAsync(job.Id);
        Assert.That(stored!.Status, Is.EqualTo(CleanupStatus.Done));
        Assert.That(stored.CompletedAt, Is.Not.Null);
    }
}
