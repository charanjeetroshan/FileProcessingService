using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Exports;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FileProcessingService.UnitTests;

public class CustomerExportServiceTests
{
    private ICustomerRepository customerRepositoryMock = null!;
    private IImportJobRepository importJobRepositoryMock = null!;
    private IExporter exporterMock = null!;
    private CustomerExportService service = null!;

    [SetUp]
    public void Setup()
    {
        customerRepositoryMock = Substitute.For<ICustomerRepository>();
        importJobRepositoryMock = Substitute.For<IImportJobRepository>();
        exporterMock = Substitute.For<IExporter>();
        exporterMock.Format.Returns(EExportFormat.NdJson);

        var options = Options.Create(new FileStorageOptions { ExportDirectoryPath = "exports" });

        service = new CustomerExportService(
            importJobRepositoryMock,
            customerRepositoryMock,
            [exporterMock],
            options,
            NullLogger<CustomerExportService>.Instance);
    }

    private static async IAsyncEnumerable<Customer> ToAsyncEnumerable(IEnumerable<Customer> customers)
    {
        foreach (var customer in customers)
        {
            yield return customer;
        }

        await Task.CompletedTask;
    }

    private void SetupJob(Guid importId, ImportStatus status)
    {
        importJobRepositoryMock.GetByIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(new ImportJob { Id = importId, Status = status });
    }

    [Test]
    public async Task ExportByImportIdAsync_JobNotFound_ReturnsErrorWithoutJob()
    {
        var importId = Guid.NewGuid();

        var result = await service.ExportByImportIdAsync(importId, EExportFormat.NdJson);

        Assert.That(result, Is.TypeOf<CustomerExportOutcome.JobNotFound>());
        customerRepositoryMock.DidNotReceiveWithAnyArgs().GetByImportIdAsync(default, default);
    }

    [Test]
    public async Task ExportByImportIdAsync_JobNotCompleted_ReturnsErrorWithJob()
    {
        var importId = Guid.NewGuid();
        SetupJob(importId, ImportStatus.Pending);

        var result = await service.ExportByImportIdAsync(importId, EExportFormat.NdJson);

        Assert.That(result, Is.TypeOf<CustomerExportOutcome.JobNotCompleted>());
        Assert.That(((CustomerExportOutcome.JobNotCompleted)result).Job.Id, Is.EqualTo(importId));
        customerRepositoryMock.DidNotReceiveWithAnyArgs().GetByImportIdAsync(default, default);
    }

    [Test]
    public async Task ExportByImportIdAsync_NoCustomers_ReturnsErrorWithJob()
    {
        var importId = Guid.NewGuid();
        SetupJob(importId, ImportStatus.Completed);
        customerRepositoryMock.GetByImportIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable([]));

        var result = await service.ExportByImportIdAsync(importId, EExportFormat.NdJson);

        Assert.That(result, Is.TypeOf<CustomerExportOutcome.NoCustomers>());
        Assert.That(((CustomerExportOutcome.NoCustomers)result).Job.Id, Is.EqualTo(importId));
    }

    [Test]
    public async Task ExportByImportIdAsync_WithCustomers_ReturnsOutcomeWithExpectedContentType()
    {
        var importId = Guid.NewGuid();
        SetupJob(importId, ImportStatus.Completed);
        var customers = new List<Customer> { new() { ImportId = importId } };
        customerRepositoryMock.GetByImportIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(customers));

        var expectedExportResult = new ExportResult("exports/file.ndjson", 1);
        exporterMock.ExportCustomers(Arg.Any<IAsyncEnumerable<Customer>>(), "exports", Arg.Any<CancellationToken>())
            .Returns(expectedExportResult);

        var result = await service.ExportByImportIdAsync(importId, EExportFormat.NdJson);

        Assert.That(result, Is.TypeOf<CustomerExportOutcome.Exported>());
        var exported = (CustomerExportOutcome.Exported)result;
        Assert.That(exported.Result, Is.EqualTo(expectedExportResult));
        Assert.That(exported.ContentType, Is.EqualTo("application/x-ndjson"));
    }

    [Test]
    public async Task ExportByImportIdAsync_UnsupportedFormat_ThrowsFileProcessingException()
    {
        var importId = Guid.NewGuid();
        SetupJob(importId, ImportStatus.Completed);
        var customers = new List<Customer> { new() { ImportId = importId } };
        customerRepositoryMock.GetByImportIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(customers));

        Assert.ThrowsAsync<InvalidOperationException>(() => service.ExportByImportIdAsync(importId, EExportFormat.Csv));
    }
}
