using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FileProcessingService.UnitTests;

public class CleanupWorkerTests
{
    private static ServiceProvider BuildServiceProvider(
        ICleanupJobRepository repositoryMock,
        IFileStorageService fileStorageMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repositoryMock);
        services.AddScoped(_ => fileStorageMock);
        return services.BuildServiceProvider();
    }

    private static async Task StopWithTimeoutAsync(CleanupWorker worker, TimeSpan timeout)
    {
        using var stopCts = new CancellationTokenSource(timeout);
        var stopTask = worker.StopAsync(stopCts.Token);

        var completed = await Task.WhenAny(stopTask, Task.Delay(timeout + TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(stopTask), "Expected the worker to stop within the timeout.");
        await stopTask;
    }

    private static CleanupJob CreateJob(string filePath) => new()
    {
        FilePath = filePath,
        Status = CleanupStatus.CleaningUp,
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Test]
    public async Task ExecuteAsync_ClaimsAndDeletesFile_MarksJobDoneAndSaves()
    {
        var job = CreateJob("C:/temp/file.csv");
        var savedSignal = new TaskCompletionSource();

        var repositoryMock = Substitute.For<ICleanupJobRepository>();
        repositoryMock.ClaimByCountAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job], []);
        repositoryMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => savedSignal.TrySetResult());

        var fileStorageMock = Substitute.For<IFileStorageService>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task), "Expected the worker to process the claimed job within the timeout.");

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        fileStorageMock.Received(1).DeleteFile(job.FilePath);
        Assert.That(job.Status, Is.EqualTo(CleanupStatus.Done));
        Assert.That(job.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task ExecuteAsync_WithNoPendingJobs_DoesNotCallDeleteFile()
    {
        var repositoryMock = Substitute.For<ICleanupJobRepository>();
        repositoryMock.ClaimByCountAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([]);

        var fileStorageMock = Substitute.For<IFileStorageService>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        fileStorageMock.DidNotReceive().DeleteFile(Arg.Any<string>());
        await repositoryMock.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenDeleteFileThrows_MarksJobFailedAndSaves()
    {
        var job = CreateJob("C:/temp/missing.csv");
        var savedSignal = new TaskCompletionSource();

        var repositoryMock = Substitute.For<ICleanupJobRepository>();
        repositoryMock.ClaimByCountAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Returns([job], []);
        repositoryMock.SaveChangesAsync(Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => savedSignal.TrySetResult());

        var fileStorageMock = Substitute.For<IFileStorageService>();
        fileStorageMock.When(f => f.DeleteFile(job.FilePath))
            .Do(_ => throw new InvalidOperationException("file not found"));

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        Assert.That(job.Status, Is.EqualTo(CleanupStatus.Failed));
        Assert.That(job.FailureReason, Is.EqualTo("file not found"));
    }

    [Test]
    public async Task ExecuteAsync_WhenClaimByCountThrows_DoesNotCrashWorker()
    {
        var repositoryMock = Substitute.For<ICleanupJobRepository>();
        repositoryMock.ClaimByCountAsync(Arg.Any<int>(), Arg.Any<CancellationToken>())
            .Throws(new InvalidOperationException("db unavailable"));

        var fileStorageMock = Substitute.For<IFileStorageService>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.DoesNotThrowAsync(async () => await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(2)));
    }
}
