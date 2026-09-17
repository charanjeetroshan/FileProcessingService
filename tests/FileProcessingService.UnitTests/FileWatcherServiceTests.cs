using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.FileStorage;
using FileProcessingService.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;

namespace FileProcessingService.UnitTests;

public class FileWatcherServiceTests
{
    private string uploadDirectory = null!;
    private string exportDirectory = null!;

    [SetUp]
    public void Setup()
    {
        uploadDirectory = Path.Combine(Path.GetTempPath(), $"filewatcher-upload-{Guid.NewGuid()}");
        exportDirectory = Path.Combine(Path.GetTempPath(), $"filewatcher-export-{Guid.NewGuid()}");
        Directory.CreateDirectory(uploadDirectory);
        Directory.CreateDirectory(exportDirectory);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(uploadDirectory))
        {
            Directory.Delete(uploadDirectory, recursive: true);
        }

        if (Directory.Exists(exportDirectory))
        {
            Directory.Delete(exportDirectory, recursive: true);
        }
    }

    private ServiceProvider BuildServiceProvider(
        Mock<ICleanupJobRepository> cleanupJobRepositoryMock,
        Mock<IImportJobRepository> importJobRepositoryMock,
        out IOptions<FileStorageOptions> options)
    {
        var fileStorageOptions = new FileStorageOptions
        {
            UploadDirectoryPath = uploadDirectory,
            ExportDirectoryPath = exportDirectory,
            FileKeepDurationInDays = 30
        };
        options = Options.Create(fileStorageOptions);

        var services = new ServiceCollection();
        services.AddScoped(_ => cleanupJobRepositoryMock.Object);
        services.AddScoped(_ => importJobRepositoryMock.Object);
        services.AddScoped<IFileStorageService>(_ => new LocalFileStorageService(NullLogger<LocalFileStorageService>.Instance));
        return services.BuildServiceProvider();
    }

    private static async Task StopWithTimeoutAsync(FileWatcherService worker, TimeSpan timeout)
    {
        using var stopCts = new CancellationTokenSource(timeout);
        var stopTask = worker.StopAsync(stopCts.Token);

        var completed = await Task.WhenAny(stopTask, Task.Delay(timeout + TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(stopTask), "Expected the worker to stop within the timeout.");
        await stopTask;
    }

    private static Mock<ICleanupJobRepository> CreateDefaultCleanupJobRepositoryMock(TaskCompletionSource savedSignal)
    {
        var mock = new Mock<ICleanupJobRepository>();
        mock.Setup(r => r.GetExistingCleanupFilePathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        mock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => savedSignal.TrySetResult());
        return mock;
    }

    [Test]
    public async Task ExecuteAsync_CreatesCleanupJobForCompletedUploadedFile()
    {
        var storedFileName = "stored.csv";
        await File.WriteAllTextAsync(Path.Combine(uploadDirectory, storedFileName), "content");

        var completedJob = new ImportJob
        {
            OriginalFileName = "file.csv",
            StoredFileName = storedFileName,
            Status = ImportStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };

        var savedSignal = new TaskCompletionSource();
        var cleanupJobRepositoryMock = CreateDefaultCleanupJobRepositoryMock(savedSignal);
        List<CleanupJob>? addedJobs = null;
        cleanupJobRepositoryMock.Setup(r => r.AddJobsAsync(It.IsAny<IEnumerable<CleanupJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<IEnumerable<CleanupJob>, CancellationToken>((jobs, _) => addedJobs = [.. jobs]);

        var importJobRepositoryMock = new Mock<IImportJobRepository>();
        importJobRepositoryMock.Setup(r => r.GetCompletedJobsByFileNamesAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([completedJob]);
        importJobRepositoryMock.Setup(r => r.GetAllTrackedFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([storedFileName]);

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task), "Expected a scan cycle to complete within the timeout.");

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(15));

        Assert.That(addedJobs, Is.Not.Null);
        Assert.That(addedJobs!.Select(j => j.FilePath), Has.Member(Path.Combine(uploadDirectory, storedFileName)));
    }

    [Test]
    public async Task ExecuteAsync_SkipsUploadedFileAlreadyTrackedForCleanup()
    {
        var storedFileName = "stored.csv";
        var filePath = Path.Combine(uploadDirectory, storedFileName);
        await File.WriteAllTextAsync(filePath, "content");

        var completedJob = new ImportJob
        {
            OriginalFileName = "file.csv",
            StoredFileName = storedFileName,
            Status = ImportStatus.Completed,
            CompletedAt = DateTimeOffset.UtcNow
        };

        var scanCompletedSignal = new TaskCompletionSource();
        var cleanupJobRepositoryMock = new Mock<ICleanupJobRepository>();
        cleanupJobRepositoryMock.Setup(r => r.GetExistingCleanupFilePathsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([filePath]);
        cleanupJobRepositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => scanCompletedSignal.TrySetResult());

        var importJobRepositoryMock = new Mock<IImportJobRepository>();
        importJobRepositoryMock.Setup(r => r.GetCompletedJobsByFileNamesAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([completedJob]);
        importJobRepositoryMock.Setup(r => r.GetAllTrackedFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([storedFileName]);

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(scanCompletedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(scanCompletedSignal.Task));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(15));

        cleanupJobRepositoryMock.Verify(r => r.AddJobsAsync(It.IsAny<IEnumerable<CleanupJob>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_CreatesCleanupJobForAnonymousFile()
    {
        var fileName = "anonymous.csv";
        var filePath = Path.Combine(uploadDirectory, fileName);
        await File.WriteAllTextAsync(filePath, "content");

        var savedSignal = new TaskCompletionSource();
        var cleanupJobRepositoryMock = CreateDefaultCleanupJobRepositoryMock(savedSignal);
        List<CleanupJob>? addedJobs = null;
        cleanupJobRepositoryMock.Setup(r => r.AddJobsAsync(It.IsAny<IEnumerable<CleanupJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<IEnumerable<CleanupJob>, CancellationToken>((jobs, _) => addedJobs = [.. jobs]);

        var importJobRepositoryMock = new Mock<IImportJobRepository>();
        importJobRepositoryMock.Setup(r => r.GetCompletedJobsByFileNamesAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        importJobRepositoryMock.Setup(r => r.GetAllTrackedFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(15));

        Assert.That(addedJobs, Is.Not.Null);
        Assert.That(addedJobs!.Select(j => j.FilePath), Has.Member(filePath));
    }

    [Test]
    public async Task ExecuteAsync_DoesNotTreatActivelyProcessingFileAsAnonymous()
    {
        var storedFileName = "processing.csv";
        var filePath = Path.Combine(uploadDirectory, storedFileName);
        await File.WriteAllTextAsync(filePath, "content");

        var savedSignal = new TaskCompletionSource();
        var cleanupJobRepositoryMock = CreateDefaultCleanupJobRepositoryMock(savedSignal);
        List<CleanupJob>? addedJobs = null;
        cleanupJobRepositoryMock.Setup(r => r.AddJobsAsync(It.IsAny<IEnumerable<CleanupJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<IEnumerable<CleanupJob>, CancellationToken>((jobs, _) => addedJobs = [.. jobs]);

        var importJobRepositoryMock = new Mock<IImportJobRepository>();
        importJobRepositoryMock.Setup(r => r.GetCompletedJobsByFileNamesAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        // The file has a tracked (in-progress) import job, so it must not be treated as anonymous.
        importJobRepositoryMock.Setup(r => r.GetAllTrackedFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([storedFileName]);

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(15));

        Assert.That(addedJobs is null || addedJobs.Count == 0, Is.True, "File belonging to an in-progress import should not be scheduled for cleanup.");
    }

    [Test]
    public async Task ExecuteAsync_CreatesCleanupJobForExpiredExportedFile()
    {
        var filePath = Path.Combine(exportDirectory, "old-export.json");
        await File.WriteAllTextAsync(filePath, "content");
        File.SetCreationTimeUtc(filePath, DateTime.UtcNow.AddDays(-40));

        var savedSignal = new TaskCompletionSource();
        var cleanupJobRepositoryMock = CreateDefaultCleanupJobRepositoryMock(savedSignal);
        List<CleanupJob>? addedJobs = null;
        cleanupJobRepositoryMock.Setup(r => r.AddJobsAsync(It.IsAny<IEnumerable<CleanupJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<IEnumerable<CleanupJob>, CancellationToken>((jobs, _) => addedJobs = [.. jobs]);

        var importJobRepositoryMock = new Mock<IImportJobRepository>();
        importJobRepositoryMock.Setup(r => r.GetCompletedJobsByFileNamesAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        importJobRepositoryMock.Setup(r => r.GetAllTrackedFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(15));

        Assert.That(addedJobs, Is.Not.Null);
        Assert.That(addedJobs!.Select(j => j.FilePath), Has.Member(filePath));
    }

    [Test]
    public async Task ExecuteAsync_DoesNotCreateCleanupJobForRecentExportedFile()
    {
        var filePath = Path.Combine(exportDirectory, "recent-export.json");
        await File.WriteAllTextAsync(filePath, "content");
        File.SetCreationTimeUtc(filePath, DateTime.UtcNow);

        var savedSignal = new TaskCompletionSource();
        var cleanupJobRepositoryMock = CreateDefaultCleanupJobRepositoryMock(savedSignal);
        List<CleanupJob>? addedJobs = null;
        cleanupJobRepositoryMock.Setup(r => r.AddJobsAsync(It.IsAny<IEnumerable<CleanupJob>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback<IEnumerable<CleanupJob>, CancellationToken>((jobs, _) => addedJobs = [.. jobs]);

        var importJobRepositoryMock = new Mock<IImportJobRepository>();
        importJobRepositoryMock.Setup(r => r.GetCompletedJobsByFileNamesAsync(It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        importJobRepositoryMock.Setup(r => r.GetAllTrackedFileNamesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(15));

        Assert.That(addedJobs is null || addedJobs.Count == 0, Is.True, "Recently exported file should not be scheduled for cleanup yet.");
    }

    [Test]
    public async Task ExecuteAsync_WhenRepositoryThrows_DoesNotCrashWorker()
    {
        var cleanupJobRepositoryMock = new Mock<ICleanupJobRepository>();
        cleanupJobRepositoryMock.Setup(r => r.GetExistingCleanupFilePathsAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var importJobRepositoryMock = new Mock<IImportJobRepository>();

        using var serviceProvider = BuildServiceProvider(cleanupJobRepositoryMock, importJobRepositoryMock, out var options);
        var worker = new FileWatcherService(options, NullLogger<FileWatcherService>.Instance, serviceProvider.GetRequiredService<IServiceScopeFactory>());

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.DoesNotThrowAsync(async () => await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(2)));
    }
}
