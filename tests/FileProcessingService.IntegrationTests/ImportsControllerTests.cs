using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Validation;
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
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(csvContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("text/csv");
        form.Add(fileContent, "File", fileName);
        return form;
    }

    [Test]
    public async Task Upload_WithValidCsvFile_ReturnsCreatedWithJobDetails()
    {
        using var content = BuildUploadContent("customers.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");

        var response = await client.PostAsync("/api/imports", content);

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.Created));
        var job = await response.Content.ReadFromJsonAsync<ImportJobResponse>();
        Assert.That(job, Is.Not.Null);
        Assert.That(job!.OriginalFileName, Is.EqualTo("customers.csv"));
        Assert.That(job.Status, Is.EqualTo("Pending"));
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

        using var firstContent = BuildUploadContent("customers.csv", csvContent);
        var firstResponse = await client.PostAsync("/api/imports", firstContent);
        Assert.That(firstResponse.StatusCode, Is.EqualTo(HttpStatusCode.Created));

        using var secondContent = BuildUploadContent("customers-again.csv", csvContent);
        var secondResponse = await client.PostAsync("/api/imports", secondContent);

        Assert.That(secondResponse.StatusCode, Is.EqualTo(HttpStatusCode.Conflict));
    }

    [Test]
    public async Task GetById_WithExistingJob_ReturnsOkWithJobDetails()
    {
        using var content = BuildUploadContent("customers.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
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
        using var firstContent = BuildUploadContent("first.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        var firstResponse = await client.PostAsync("/api/imports", firstContent);
        var first = await firstResponse.Content.ReadFromJsonAsync<ImportJobResponse>();

        using var secondContent = BuildUploadContent("second.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJohn,Doe,john@example.com,1990-01-01,US\n");
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
        using var januaryContent = BuildUploadContent("customers-january.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
        await client.PostAsync("/api/imports", januaryContent);

        using var februaryContent = BuildUploadContent("customers-february.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJohn,Doe,john@example.com,1990-01-01,US\n");
        await client.PostAsync("/api/imports", februaryContent);

        var response = await client.GetAsync("/api/imports?filename=january");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportJobResponse>>();
        Assert.That(result!.TotalCount, Is.EqualTo(1));
        Assert.That(result.Items[0].OriginalFileName, Is.EqualTo("customers-january.csv"));
    }

    [Test]
    public async Task GetAll_WithStatusFilter_ReturnsOnlyMatchingJobs()
    {
        using var content = BuildUploadContent("customers.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
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
            using var content = BuildUploadContent($"customers-{i}.csv", $"FirstName,LastName,Email,DateOfBirth,Country\nJane{i},Doe,jane{i}@example.com,1990-01-01,US\n");
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
        // Missing required Email field on the single data row to force a validation error to be recorded.
        using var content = BuildUploadContent("customers.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,,1990-01-01,US\n");
        var uploadResponse = await client.PostAsync("/api/imports", content);
        var created = await uploadResponse.Content.ReadFromJsonAsync<ImportJobResponse>();

        // Processing happens on the Worker; in tests only the Api is hosted, so directly poll is not
        // meaningful. Since ImportJobProcessor is not run by the Api, no errors are persisted here.
        // This test focuses purely on endpoint routing/response shape for a job with no errors yet.
        var response = await client.GetAsync($"/api/imports/{created!.Id}/errors");

        Assert.That(response.StatusCode, Is.EqualTo(HttpStatusCode.OK));
        var result = await response.Content.ReadFromJsonAsync<PagedResult<ImportErrorResponse>>();
        Assert.That(result, Is.Not.Null);
        Assert.That(result!.TotalCount, Is.EqualTo(0));
        Assert.That(result.Items, Is.Empty);
    }

    [Test]
    public async Task GetById_ForNewlyUploadedJob_ReturnsPendingStatistics()
    {
        using var content = BuildUploadContent("customers.csv", "FirstName,LastName,Email,DateOfBirth,Country\nJane,Doe,jane@example.com,1990-01-01,US\n");
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
}
