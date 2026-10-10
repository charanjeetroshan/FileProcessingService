using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.Net;

namespace FileProcessingService.IntegrationTests;

[TestFixture]
[Explicit("Occasionally long running tests")]
public class ExportsControllerTests
{
    private CustomWebApplicationFactory factory = null!;
    private HttpClient client = null!;

    [SetUp]
    public void Setup()
    {
        factory = new CustomWebApplicationFactory();
        client = factory.CreateClient();
    }

    [TearDown]
    public void TearDown()
    {
        client.Dispose();
        factory.Dispose();
    }

    private Task<Guid> SeedCustomersAsync(params Customer[] customers) =>
        SeedCustomersAsync(ImportStatus.Completed, customers);

    private async Task<Guid> SeedCustomersAsync(ImportStatus status, params Customer[] customers)
    {
        var importId = Guid.NewGuid();

        using var scope = factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>();

        dbContext.ImportJobs.Add(new ImportJob
        {
            Id = importId,
            DataPackageGuid = Guid.NewGuid().ToString(),
            CompositeHash = Guid.NewGuid().ToString(),
            UploadedFileName = "test.csv",
            Status = status,
            CreatedAt = DateTimeOffset.UtcNow
        });

        foreach (var customer in customers)
        {
            customer.ImportId = importId;
            dbContext.Customers.Add(customer);
        }

        await dbContext.SaveChangesAsync();

        return importId;
    }

    private static Customer BuildCustomer(string email) => new()
    {
        FirstName = "Jane",
        LastName = "Doe",
        Email = email,
        DateOfBirth = new DateOnly(1990, 1, 1),
        Country = "US",
        CreatedAt = DateTimeOffset.UtcNow
    };

    [Test]
    public async Task ExportByImportId_WithExistingCustomers_ReturnsOkWithNdJsonFile()
    {
        var importId = await SeedCustomersAsync(BuildCustomer("jane.doe@example.com"), BuildCustomer("john.doe@example.com"));

        var response = await client.GetAsync($"/api/exports/{importId}?format=NdJson");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        Assert.That(response.Content.Headers.ContentType?.MediaType, Is.EqualTo("application/x-ndjson"));

        var content = await response.Content.ReadAsStringAsync();
        var lines = content.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.That(lines, Has.Length.EqualTo(2));
    }

    [Test]
    public async Task ExportByImportId_WithUnknownJob_ReturnsNotFound()
    {
        var response = await client.GetAsync($"/api/exports/{Guid.NewGuid()}?format=NdJson");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task ExportByImportId_WithNoCustomers_ReturnsNoContent()
    {
        var importId = await SeedCustomersAsync();

        var response = await client.GetAsync($"/api/exports/{importId}?format=NdJson");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NoContent));
    }

    [Test]
    public async Task ExportByImportId_WithJobNotCompleted_ReturnsBadRequest()
    {
        var importId = await SeedCustomersAsync(ImportStatus.Pending, BuildCustomer("jane.doe@example.com"));

        var response = await client.GetAsync($"/api/exports/{importId}?format=NdJson");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ExportByImportId_WithInvalidFormat_ReturnsBadRequest()
    {
        var importId = await SeedCustomersAsync(BuildCustomer("jane.doe@example.com"));

        var response = await client.GetAsync($"/api/exports/{importId}?format=Unknown");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task ExportByImportId_CreatesFileInExportDirectory()
    {
        var importId = await SeedCustomersAsync(BuildCustomer("jane.doe@example.com"));

        var response = await client.GetAsync($"/api/exports/{importId}?format=NdJson");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var exportedFiles = Directory.GetFiles(factory.ExportDirectoryPath);
        Assert.That(exportedFiles, Has.Length.EqualTo(1));
        Assert.That(exportedFiles[0], Does.EndWith(".ndjson"));
    }
}
