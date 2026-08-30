using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace FileProcessingService.UnitTests;

public class ImportWorkerTests
{
    private static ServiceProvider BuildServiceProvider(
        Mock<IImportJobRepository> repositoryMock,
        Mock<IImportJobProcessor> processorMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repositoryMock.Object);
        services.AddScoped(_ => processorMock.Object);
        return services.BuildServiceProvider();
    }

    private static async Task StopWithTimeoutAsync(ImportWorker worker, TimeSpan timeout)
    {
        using var stopCts = new CancellationTokenSource(timeout);
        var stopTask = worker.StopAsync(stopCts.Token);

        var completed = await Task.WhenAny(stopTask, Task.Delay(timeout + TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(stopTask), "Expected the worker to stop within the timeout.");
        await stopTask;
    }

    [Test]
    public async Task ExecuteAsync_ClaimsAndProcessesAvailableJob()
    {
        var job = new ImportJob { OriginalFileName = "file.csv", StoredFileName = "stored.csv" };
        var processedSignal = new TaskCompletionSource();

        var repositoryMock = new Mock<IImportJobRepository>();
        repositoryMock.SetupSequence(r => r.ClaimNextPendingJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job)
            .ReturnsAsync((ImportJob?)null);
        repositoryMock.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var processorMock = new Mock<IImportJobProcessor>();
        processorMock.Setup(p => p.ProcessJob(job, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask)
            .Callback(() => processedSignal.TrySetResult());

        using var serviceProvider = BuildServiceProvider(repositoryMock, processorMock);
        var worker = new ImportWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(processedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(processedSignal.Task), "Expected the worker to process the claimed job within the timeout.");

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        processorMock.Verify(p => p.ProcessJob(job, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Test]
    public async Task ExecuteAsync_WithNoPendingJobs_DoesNotInvokeProcessor()
    {
        var repositoryMock = new Mock<IImportJobRepository>();
        repositoryMock.Setup(r => r.ClaimNextPendingJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((ImportJob?)null);

        var processorMock = new Mock<IImportJobProcessor>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, processorMock);
        var worker = new ImportWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        processorMock.Verify(p => p.ProcessJob(It.IsAny<ImportJob>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Test]
    public async Task ExecuteAsync_WhenProcessorThrows_DoesNotCrashWorker()
    {
        var job = new ImportJob { OriginalFileName = "file.csv", StoredFileName = "stored.csv" };
        var attemptedSignal = new TaskCompletionSource();

        var repositoryMock = new Mock<IImportJobRepository>();
        repositoryMock.SetupSequence(r => r.ClaimNextPendingJobAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(job)
            .ReturnsAsync((ImportJob?)null);
        repositoryMock.Setup(r => r.GetByIdAsync(job.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(job);

        var processorMock = new Mock<IImportJobProcessor>();
        processorMock.Setup(p => p.ProcessJob(job, It.IsAny<CancellationToken>()))
            .Callback(() => attemptedSignal.TrySetResult())
            .ThrowsAsync(new InvalidOperationException("boom"));

        using var serviceProvider = BuildServiceProvider(repositoryMock, processorMock);
        var worker = new ImportWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(attemptedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(attemptedSignal.Task));

        Assert.DoesNotThrowAsync(async () => await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(2)));
    }
}
