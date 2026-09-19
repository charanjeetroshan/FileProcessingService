using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Exports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Exceptions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace FileProcessingService.UnitTests;

public class CustomerExportServiceTests
{
    private ICustomerRepository customerRepositoryMock = null!;
    private IExporter exporterMock = null!;
    private CustomerExportService service = null!;

    [SetUp]
    public void Setup()
    {
        customerRepositoryMock = Substitute.For<ICustomerRepository>();
        exporterMock = Substitute.For<IExporter>();
        exporterMock.Format.Returns(EExportFormat.NdJson);

        var options = Options.Create(new FileStorageOptions { ExportDirectoryPath = "exports" });

        service = new CustomerExportService(
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

    [Test]
    public async Task ExportByImportIdAsync_NoCustomers_ReturnsNull()
    {
        var importId = Guid.NewGuid();
        customerRepositoryMock.GetByImportIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable([]));

        var result = await service.ExportByImportIdAsync(importId, EExportFormat.NdJson);

        Assert.That(result, Is.Null);
    }

    [Test]
    public async Task ExportByImportIdAsync_WithCustomers_ReturnsOutcomeWithExpectedContentType()
    {
        var importId = Guid.NewGuid();
        var customers = new List<Customer> { new() { ImportId = importId } };
        customerRepositoryMock.GetByImportIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(customers));

        var expectedExportResult = new ExportResult("exports/file.ndjson", 1);
        exporterMock.ExportCustomers(Arg.Any<IAsyncEnumerable<Customer>>(), "exports", Arg.Any<CancellationToken>())
            .Returns(expectedExportResult);

        var result = await service.ExportByImportIdAsync(importId, EExportFormat.NdJson);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Result, Is.EqualTo(expectedExportResult));
        Assert.That(result.ContentType, Is.EqualTo("application/x-ndjson"));
    }

    [Test]
    public async Task ExportByImportIdAsync_UnsupportedFormat_ThrowsFileProcessingException()
    {
        var importId = Guid.NewGuid();
        var customers = new List<Customer> { new() { ImportId = importId } };
        customerRepositoryMock.GetByImportIdAsync(importId, Arg.Any<CancellationToken>())
            .Returns(_ => ToAsyncEnumerable(customers));

        Assert.ThrowsAsync<FileProcessingException>(() => service.ExportByImportIdAsync(importId, EExportFormat.Csv));
    }
}
