using FileProcessingService.Application.Exports;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Exports;
using System.Text.Json;

namespace FileProcessingService.UnitTests;

public class NdJsonExporterTests
{
    private string exportDirectory = null!;
    private NdJsonExporter exporter = null!;

    [SetUp]
    public void Setup()
    {
        exportDirectory = Path.Combine(Path.GetTempPath(), $"ndjson-exporter-tests-{Guid.NewGuid()}");
        exporter = new NdJsonExporter();
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(exportDirectory))
        {
            Directory.Delete(exportDirectory, recursive: true);
        }
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
    public void Format_IsNdJson()
    {
        Assert.That(exporter.Format, Is.EqualTo(EExportFormat.NdJson));
    }

    [Test]
    public async Task ExportCustomers_CreatesDestinationDirectoryWhenMissing()
    {
        var customers = ToAsyncEnumerable([]);

        await exporter.ExportCustomers(customers, exportDirectory);

        Assert.That(Directory.Exists(exportDirectory), Is.True);
    }

    [Test]
    public async Task ExportCustomers_ReturnsPathOfCreatedFile()
    {
        var customers = ToAsyncEnumerable([]);

        var exportResult = await exporter.ExportCustomers(customers, exportDirectory);

        Assert.That(File.Exists(exportResult.ExportedFilePath), Is.True);
        Assert.That(Path.GetDirectoryName(exportResult.ExportedFilePath), Is.EqualTo(exportDirectory));
        Assert.That(Path.GetExtension(exportResult.ExportedFilePath), Is.EqualTo(".ndjson"));
    }

    [Test]
    public async Task ExportCustomers_WritesOneJsonLinePerCustomer()
    {
        var customers = new List<Customer>
        {
            new()
            {
                FirstName = "Jane",
                LastName = "Doe",
                Email = "jane.doe@example.com",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Country = "US",
                CreatedAt = DateTimeOffset.UtcNow
            },
            new()
            {
                FirstName = "John",
                LastName = "Smith",
                Email = "john.smith@example.com",
                DateOfBirth = new DateOnly(1985, 5, 5),
                Country = "UK",
                CreatedAt = DateTimeOffset.UtcNow
            }
        };

        var exportResult = await exporter.ExportCustomers(ToAsyncEnumerable(customers), exportDirectory);

        var lines = await File.ReadAllLinesAsync(exportResult.ExportedFilePath);
        Assert.That(lines, Has.Length.EqualTo(2));

        var firstCustomer = JsonSerializer.Deserialize<Customer>(lines[0]);
        Assert.That(firstCustomer!.Email, Is.EqualTo("jane.doe@example.com"));

        var secondCustomer = JsonSerializer.Deserialize<Customer>(lines[1]);
        Assert.That(secondCustomer!.Email, Is.EqualTo("john.smith@example.com"));
    }

    [Test]
    public async Task ExportCustomers_WithNoCustomers_CreatesEmptyFile()
    {
        var exportResult = await exporter.ExportCustomers(ToAsyncEnumerable([]), exportDirectory);

        var content = await File.ReadAllTextAsync(exportResult.ExportedFilePath);
        Assert.That(content, Is.Empty);
    }

    [Test]
    public void ExportCustomers_WithCancelledToken_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var customers = ToAsyncEnumerable([new Customer()]);

        Assert.ThrowsAsync<TaskCanceledException>(async () =>
            await exporter.ExportCustomers(customers, exportDirectory, cts.Token));
    }
}
