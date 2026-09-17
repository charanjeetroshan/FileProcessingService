using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileProcessingService.UnitTests;

public class CleanupWorkerTests
{
    private static ServiceProvider BuildServiceProvider(
        Mock<ICleanupJobRepository> repositoryMock,
        Mock<IFileStorageService> fileStorageMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repositoryMock.Object);
        services.AddScoped(_ => fileStorageMock.Object);
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

        var repositoryMock = new Mock<ICleanupJobRepository>();
        repositoryMock.SetupSequence(r => r.ClaimByCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([job])
            .ReturnsAsync([]);
        repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => savedSignal.TrySetResult());

        var fileStorageMock = new Mock<IFileStorageService>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(savedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(savedSignal.Task), "Expected the worker to process the claimed job within the timeout.");

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        fileStorageMock.Verify(f => f.DeleteFile(job.FilePath), Times.Once);
        Assert.That(job.Status, Is.EqualTo(CleanupStatus.Done));
        Assert.That(job.CompletedAt, Is.Not.Null);
    }

    [Test]
    public async Task ExecuteAsync_WithNoPendingJobs_DoesNotCallDeleteFile()
    {
        var repositoryMock = new Mock<ICleanupJobRepository>();
        repositoryMock.Setup(r => r.ClaimByCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var fileStorageMock = new Mock<IFileStorageService>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        fileStorageMock.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
        repositoryMock.Verify(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenDeleteFileThrows_MarksJobFailedAndSaves()
    {
        var job = CreateJob("C:/temp/missing.csv");
        var savedSignal = new TaskCompletionSource();

        var repositoryMock = new Mock<ICleanupJobRepository>();
        repositoryMock.SetupSequence(r => r.ClaimByCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([job])
            .ReturnsAsync([]);
        repositoryMock.Setup(r => r.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => savedSignal.TrySetResult());

        var fileStorageMock = new Mock<IFileStorageService>();
        fileStorageMock.Setup(f => f.DeleteFile(job.FilePath))
            .Throws(new InvalidOperationException("file not found"));

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
        var repositoryMock = new Mock<ICleanupJobRepository>();
        repositoryMock.Setup(r => r.ClaimByCountAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("db unavailable"));

        var fileStorageMock = new Mock<IFileStorageService>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, fileStorageMock);
        var worker = new CleanupWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<CleanupWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        Assert.DoesNotThrowAsync(async () => await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(2)));
    }
}
