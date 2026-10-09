using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Validation;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Domain.Constants;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;

namespace FileProcessingService.IntegrationTests;

[TestFixture]
[Explicit("Occasionally long running tests")]
public class ImportsControllerTests
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

    private static MultipartFormDataContent BuildUploadContent(string fileName, string csvContent)
        => BuildArchiveContent(fileName, (FileSystemConstants.CustomerFileName, csvContent));

    private static MultipartFormDataContent BuildArchiveContent(string fileName, params (string Name, string Content)[] files)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var file in files)
            {
                using var writer = new StreamWriter(archive.CreateEntry(file.Name).Open(), new UTF8Encoding(false));
                writer.Write(file.Content);
            }
        }
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(stream.ToArray());
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("application/zip");
        form.Add(fileContent, "File", fileName);
        return form;
    }

    [Test]
    public async Task Upload_WithValidArchive_ReturnsCreatedWithJobDetails()
    {
        using var content = BuildUploadContent("customers.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");

        var response = await client.PostAsync("/api/imports", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var job = await response.Content.ReadFromJsonAsync<ImportJobResponse>();
        Assert.That(job, Is.Not.Null);
        Assert.That(job!.UploadedFileName, Is.EqualTo("customers.zip"));
        Assert.That(job.Status, Is.EqualTo("Pending"));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>();
        var stored = await db.ImportJobs.FindAsync(job.Id);
        var packageDirectory = Path.Combine(factory.UploadDirectoryPath, stored!.DataPackageGuid);
        Assert.That(File.Exists(Path.Combine(packageDirectory, "data", "customers.csv")), Is.True);
        Assert.That(File.Exists(Path.Combine(packageDirectory, "archive", "customers.zip")), Is.True);
    }

    [Test]
    public async Task Upload_WithoutFile_ReturnsBadRequestWithValidationError()
    {
        using var form = new MultipartFormDataContent();

        var response = await client.PostAsync("/api/imports", form);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        var error = await response.Content.ReadFromJsonAsync<ValidationError>();
        Assert.That(error, Is.Not.Null);
        Assert.That(error!.Errors, Is.Not.Null.And.Not.Empty);
    }

    [Test]
    public async Task Upload_WithInvalidExtension_ReturnsBadRequest()
    {
        using var content = BuildUploadContent("customers.txt", "not-a-csv");

        var response = await client.PostAsync("/api/imports", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
    }

    [Test]
    public async Task Upload_WithDuplicateFileContent_ReturnsConflictOnSecondUpload()
    {
        var csvContent = "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n";

        using var firstContent = BuildUploadContent("customers.zip", csvContent);
        var firstResponse = await client.PostAsync("/api/imports", firstContent);
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var secondContent = BuildArchiveContent("customers-again.zip", ("notes.txt", "ignored metadata"), ("CUSTOMERS.CSV", csvContent));
        var secondResponse = await client.PostAsync("/api/imports", secondContent);

        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
        var first = await firstResponse.Content.ReadFromJsonAsync<ImportJobResponse>();
        var duplicate = await secondResponse.Content.ReadFromJsonAsync<ImportJobResponse>();
        Assert.That(duplicate!.Id, Is.EqualTo(first!.Id));
        Assert.That(Directory.GetDirectories(factory.UploadDirectoryPath), Has.Length.EqualTo(1));
        Assert.That(Directory.GetFiles(factory.UploadDirectoryPath), Is.Empty);
    }

    [Test]
    public async Task GetById_WithExistingJob_ReturnsOkWithJobDetails()
    {
        using var content = BuildUploadContent("customers.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        var uploadResponse = await client.PostAsync("/api/imports", content);
        var created = await uploadResponse.Content.ReadFromJsonAsync<ImportJobResponse>();

        var response = await client.GetAsync($"/api/imports/{created!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var job = await response.Content.ReadFromJsonAsync<ImportJobResponse>();
        Assert.That(job!.Id, Is.EqualTo(created.Id));
    }

    [Test]
    public async Task GetById_WithNonExistentJob_ReturnsNotFound()
    {
        var response = await client.GetAsync($"/api/imports/{Guid.NewGuid()}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetAll_ReturnsUploadedJobsNewestFirst()
    {
        using var firstContent = BuildUploadContent("first.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        var firstResponse = await client.PostAsync("/api/imports", firstContent);
        var first = await firstResponse.Content.ReadFromJsonAsync<ImportJobResponse>();

        using var secondContent = BuildUploadContent("second.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJohn,Doe,john@example.com,1990-01-01,US\n");
        var secondResponse = await client.PostAsync("/api/imports", secondContent);
        var second = await secondResponse.Content.ReadFromJsonAsync<ImportJobResponse>();

        var response = await client.GetAsync("/api/imports");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportJobResponse>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TotalCount, Is.EqualTo(2));
        Assert.That(result.Items[0].Id, Is.EqualTo(second!.Id));
        Assert.That(result.Items[1].Id, Is.EqualTo(first!.Id));
    }

    [Test]
    public async Task GetAll_WithFilenameFilter_ReturnsOnlyMatchingJobs()
    {
        using var januaryContent = BuildUploadContent("customers-january.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        await client.PostAsync("/api/imports", januaryContent);

        using var februaryContent = BuildUploadContent("customers-february.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJohn,Doe,john@example.com,1990-01-01,US\n");
        await client.PostAsync("/api/imports", februaryContent);

        var response = await client.GetAsync("/api/imports?filename=january");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportJobResponse>>();
        Assert.That(result!.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items[0].UploadedFileName, Is.EqualTo("customers-january.zip"));
    }

    [Test]
    public async Task GetAll_WithStatusFilter_ReturnsOnlyMatchingJobs()
    {
        using var content = BuildUploadContent("customers.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        await client.PostAsync("/api/imports", content);

        var response = await client.GetAsync("/api/imports?status=Completed");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportJobResponse>>();
        Assert.That(result!.TotalCount, Is.EqualTo(0));
    }

    [Test]
    public async Task GetAll_WithPageSize_PaginatesResults()
    {
        for (var i = 0; i < 3; i++)
        {
            using var content = BuildUploadContent($"customers-{i}.zip", $"FirstName,LastName,Email,DateOfBirth,Country\nJane{i},Doe,jane{i}@example.com,1990-01-01,US\n");
            await client.PostAsync("/api/imports", content);
        }

        var response = await client.GetAsync("/api/imports?page=1&pageSize=2");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportJobResponse>>();
        Assert.That(result!.TotalCount, Is.EqualTo(3));
        Assert.That(result.Items, Has.Count.EqualTo(2));
        Assert.That(result.TotalPages, Is.EqualTo(2));
    }

    [Test]
    public async Task GetErrors_WithNonExistentJob_ReturnsNotFound()
    {
        var response = await client.GetAsync($"/api/imports/{Guid.NewGuid()}/errors");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.NotFound));
    }

    [Test]
    public async Task GetErrors_WithInvalidRow_ReturnsPagedValidationErrors()
    {
        using var content = BuildUploadContent("customers.zip", "FirstName;LastName;Email;DateOfBirth;Country\nJane;Doe;;1990-01-01;US\n");
        var uploadResponse = await client.PostAsync("/api/imports", content);
        Assert.That(uploadResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await uploadResponse.Content.ReadFromJsonAsync<ImportJobResponse>();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>();
        var job = await db.ImportJobs.FindAsync(created!.Id);
        await scope.ServiceProvider.GetRequiredService<IImportJobProcessor>().ProcessJob(job!, CancellationToken.None);
        var response = await client.GetAsync($"/api/imports/{created!.Id}/errors");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportErrorResponse>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TotalCount, Is.GreaterThan(0));
        Assert.That(result.Items.Any(error => error.Field == "Email"), Is.True);
    }

    [Test]
    public async Task GetById_ForNewlyUploadedJob_ReturnsPendingStatistics()
    {
        using var content = BuildUploadContent("customers.zip", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        var uploadResponse = await client.PostAsync("/api/imports", content);
        var created = await uploadResponse.Content.ReadFromJsonAsync<ImportJobResponse>();

        var response = await client.GetAsync($"/api/imports/{created!.Id}");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var job = await response.Content.ReadFromJsonAsync<ImportJobResponse>();
        Assert.That(job, Is.Not.Null);
        Assert.That(job!.Id, Is.EqualTo(created.Id));
        Assert.That(job.Status, Is.EqualTo("Pending"));
        Assert.That(job.TotalRows, Is.EqualTo(0));
        Assert.That(job.ProcessedRows, Is.EqualTo(0));
        Assert.That(job.PercentageComplete, Is.EqualTo(0));
        Assert.That(job.StartedAt, Is.Null);
        Assert.That(job.CompletedAt, Is.Null);
        Assert.That(job.ProcessingDuration, Is.Null);
    }

    [Test]
    public async Task Upload_WithDirectCsv_ReturnsBadRequest()
    {
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent("FirstName;LastName;Email;DateOfBirth;Country\n"), "File", "customers.csv");

        var response = await client.PostAsync("/api/imports", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(Directory.GetFileSystemEntries(factory.UploadDirectoryPath), Is.Empty);
    }

    [TestCase("empty")]
    [TestCase("unsupported")]
    [TestCase("nested")]
    [TestCase("empty-customers")]
    [TestCase("too-many")]
    public async Task Upload_WithInvalidArchiveContents_ReturnsBadRequestAndCleansPackage(string scenario)
    {
        (string Name, string Content)[] files = scenario switch
        {
            "empty" => [],
            "unsupported" => [("orders.csv", "orders")],
            "nested" => [("nested/customers.csv", "customers")],
            "empty-customers" => [("customers.csv", string.Empty)],
            _ => [("customers.csv", "customers"), ("notes.txt", "notes"), ("extra.txt", "extra")]
        };
        using var content = BuildArchiveContent("package.zip", files);

        var response = await client.PostAsync("/api/imports", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.BadRequest));
        Assert.That(Directory.GetFileSystemEntries(factory.UploadDirectoryPath), Is.Empty);
        using var scope = factory.Services.CreateScope();
        Assert.That(scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>().ImportJobs.Count(), Is.Zero);
    }

    [Test]
    public async Task Upload_ThenProcess_ImportsOnlyCustomersFromArchive()
    {
        using var content = BuildArchiveContent("package.zip",
            ("customers.csv", "FirstName;LastName;Email;DateOfBirth;Country\nJane;Doe;jane@example.com;1990-01-01;US\n"),
            ("notes.txt", "not customer data"));
        var response = await client.PostAsync("/api/imports", content);
        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var created = await response.Content.ReadFromJsonAsync<ImportJobResponse>();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FileProcessingDbContext>();
            var job = await db.ImportJobs.FindAsync(created!.Id);
            await scope.ServiceProvider.GetRequiredService<IImportJobProcessor>().ProcessJob(job!, CancellationToken.None);
            Assert.That(db.Customers.Single().Email, Is.EqualTo("jane@example.com"));
            Assert.That(db.ImportErrors.Count(), Is.Zero);
        }

        var result = await client.GetFromJsonAsync<ImportJobResponse>($"/api/imports/{created!.Id}");

        Assert.That(result!.Status, Is.EqualTo("Completed"));
        Assert.That(result.TotalRows, Is.EqualTo(1));
        Assert.That(result.SuccessfulRows, Is.EqualTo(1));
        Assert.That(result.FailedRows, Is.Zero);
    }
}
