using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace FileProcessingService.UnitTests;

public class ImportWorkerTests
{
    private static ServiceProvider BuildServiceProvider(
        IImportJobRepository repositoryMock,
        IImportJobProcessor processorMock)
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => repositoryMock);
        services.AddScoped(_ => processorMock);
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

        var repositoryMock = Substitute.For<IImportJobRepository>();
        repositoryMock.ClaimNextPendingJobAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                job.Status = ImportStatus.Processing;
                return job;
            }, _ => (ImportJob?)null);
        repositoryMock.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(job);

        var processorMock = Substitute.For<IImportJobProcessor>();
        processorMock.ProcessJob(job, Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask)
            .AndDoes(_ => processedSignal.TrySetResult());

        using var serviceProvider = BuildServiceProvider(repositoryMock, processorMock);
        var worker = new ImportWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(processedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(processedSignal.Task), "Expected the worker to process the claimed job within the timeout.");

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        await processorMock.Received(1).ProcessJob(job, Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WithNoPendingJobs_DoesNotInvokeProcessor()
    {
        var repositoryMock = Substitute.For<IImportJobRepository>();
        repositoryMock.ClaimNextPendingJobAsync(Arg.Any<CancellationToken>())
            .Returns((ImportJob?)null);

        var processorMock = Substitute.For<IImportJobProcessor>();

        using var serviceProvider = BuildServiceProvider(repositoryMock, processorMock);
        var worker = new ImportWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);
        await Task.Delay(TimeSpan.FromMilliseconds(500));

        await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(10));

        await processorMock.DidNotReceive().ProcessJob(Arg.Any<ImportJob>(), Arg.Any<CancellationToken>());
    }

    [Test]
    public async Task ExecuteAsync_WhenProcessorThrows_DoesNotCrashWorker()
    {
        var job = new ImportJob { OriginalFileName = "file.csv", StoredFileName = "stored.csv" };
        var attemptedSignal = new TaskCompletionSource();

        var repositoryMock = Substitute.For<IImportJobRepository>();
        repositoryMock.ClaimNextPendingJobAsync(Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                job.Status = ImportStatus.Processing;
                return job;
            }, _ => (ImportJob?)null);
        repositoryMock.GetByIdAsync(job.Id, Arg.Any<CancellationToken>())
            .Returns(job);

        var processorMock = Substitute.For<IImportJobProcessor>();
        processorMock.ProcessJob(job, Arg.Any<CancellationToken>())
            .Returns<Task>(_ =>
            {
                attemptedSignal.TrySetResult();
                throw new InvalidOperationException("boom");
            });

        using var serviceProvider = BuildServiceProvider(repositoryMock, processorMock);
        var worker = new ImportWorker(serviceProvider.GetRequiredService<IServiceScopeFactory>(), NullLogger<ImportWorker>.Instance);

        await worker.StartAsync(CancellationToken.None);

        var completed = await Task.WhenAny(attemptedSignal.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.That(completed, Is.EqualTo(attemptedSignal.Task));

        Assert.DoesNotThrowAsync(async () => await StopWithTimeoutAsync(worker, TimeSpan.FromSeconds(2)));
    }
}
