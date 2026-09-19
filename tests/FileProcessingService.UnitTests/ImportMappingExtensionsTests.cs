using FileProcessingService.Application.Contracts;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;

namespace FileProcessingService.UnitTests;

public class ImportMappingExtensionsTests
{
    [Test]
    public void ToResponse_ImportJob_MapsAllFields()
    {
        var createdAt = DateTimeOffset.UtcNow.AddMinutes(-10);
        var startedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var completedAt = DateTimeOffset.UtcNow;

        var job = new ImportJob
        {
            OriginalFileName = "customers.csv",
            Status = ImportStatus.Completed,
            TotalRows = 100,
            ProcessedRows = 100,
            SuccessfulRows = 90,
            FailedRows = 10,
            CreatedAt = createdAt,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            FailureReason = null
        };

        var response = job.ToResponse();

        Assert.That(response.Id, Is.EqualTo(job.Id));
        Assert.That(response.OriginalFileName, Is.EqualTo("customers.csv"));
        Assert.That(response.Status, Is.EqualTo(nameof(ImportStatus.Completed)));
        Assert.That(response.TotalRows, Is.EqualTo(100));
        Assert.That(response.ProcessedRows, Is.EqualTo(100));
        Assert.That(response.SuccessfulRows, Is.EqualTo(90));
        Assert.That(response.FailedRows, Is.EqualTo(10));
        Assert.That(response.PercentageComplete, Is.EqualTo(100));
        Assert.That(response.CreatedAt, Is.EqualTo(createdAt));
        Assert.That(response.StartedAt, Is.EqualTo(startedAt));
        Assert.That(response.CompletedAt, Is.EqualTo(completedAt));
        Assert.That(response.ProcessingDuration, Is.EqualTo(completedAt - startedAt));
        Assert.That(response.FailureReason, Is.Null);
    }

    [Test]
    public void ToResponse_ImportJob_WithZeroTotalRows_PercentageIsZero()
    {
        var job = new ImportJob { OriginalFileName = "empty.csv", TotalRows = 0, ProcessedRows = 0 };

        var response = job.ToResponse();

        Assert.That(response.PercentageComplete, Is.EqualTo(0));
    }

    [Test]
    public void ToResponse_ImportJob_WithoutStartedOrCompletedAt_ProcessingDurationIsNull()
    {
        var job = new ImportJob { OriginalFileName = "pending.csv", StartedAt = null, CompletedAt = null };

        var response = job.ToResponse();

        Assert.That(response.ProcessingDuration, Is.Null);
    }

    [Test]
    public void ToResponse_ImportJob_WithStartedButNotCompleted_ProcessingDurationIsNull()
    {
        var job = new ImportJob { OriginalFileName = "in-progress.csv", StartedAt = DateTimeOffset.UtcNow, CompletedAt = null };

        var response = job.ToResponse();

        Assert.That(response.ProcessingDuration, Is.Null);
    }

    [Test]
    public void ToResponse_ImportError_MapsAllFields()
    {
        var error = new ImportError
        {
            RowNumber = 5,
            Field = "Email",
            ErrorCode = "INVALID_EMAIL",
            ErrorMessage = "Email is not valid.",
            RawValue = "not-an-email"
        };

        var response = error.ToResponse();

        Assert.That(response.Id, Is.EqualTo(error.Id));
        Assert.That(response.RowNumber, Is.EqualTo(5));
        Assert.That(response.Field, Is.EqualTo("Email"));
        Assert.That(response.ErrorCode, Is.EqualTo("INVALID_EMAIL"));
        Assert.That(response.ErrorMessage, Is.EqualTo("Email is not valid."));
        Assert.That(response.RawValue, Is.EqualTo("not-an-email"));
    }
}
