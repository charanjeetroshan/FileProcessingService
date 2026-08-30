using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Validation.Validators;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Domain.Enums;

namespace FileProcessingService.UnitTests;

public class CustomerImportRowValidatorTests
{
    private CustomerImportRowValidator validator = null!;

    [SetUp]
    public void Setup()
    {
        validator = new CustomerImportRowValidator();
    }

    [Test]
    public void Validate_WithAllRequiredFields_IsValid()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 1,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = "1990-01-01",
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_WithMissingEmail_IsInvalid()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 2,
            FirstName = "Jane",
            LastName = "Doe",
            Email = string.Empty,
            DateOfBirth = "1990-01-01",
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_WithFirstNameExceedingMaxLength_IsInvalid()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 3,
            FirstName = new string('A', 101),
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = "1990-01-01",
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_WithEmailExceedingMaxLength_IsInvalid()
    {
        var localPart = new string('a', 315);
        var row = new CustomerImportRow
        {
            RowNumber = 4,
            FirstName = "Jane",
            LastName = "Doe",
            Email = $"{localPart}@example.com",
            DateOfBirth = "1990-01-01",
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_WithInvalidDateOfBirthFormat_IsInvalid()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 5,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = "not-a-date",
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_WithFutureDateOfBirth_IsInvalid()
    {
        var futureDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        var row = new CustomerImportRow
        {
            RowNumber = 6,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = futureDate.ToString("yyyy-MM-dd"),
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_WithCustomerUnder18_IsInvalid()
    {
        var underageDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-17);
        var row = new CustomerImportRow
        {
            RowNumber = 7,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = underageDate.ToString("yyyy-MM-dd"),
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [Test]
    public void Validate_WithCustomerExactly18_IsValid()
    {
        var eighteenthBirthday = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-18);
        var row = new CustomerImportRow
        {
            RowNumber = 8,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = eighteenthBirthday.ToString("yyyy-MM-dd"),
            Country = "US"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_WithInvalidCountryCode_IsInvalid()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 9,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = "1990-01-01",
            Country = "USA"
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.False);
    }

    [TestCase("DE")]
    [TestCase("US")]
    [TestCase("GB")]
    [TestCase("FR")]
    public void Validate_WithValidCountryCodes_IsValid(string countryCode)
    {
        var row = new CustomerImportRow
        {
            RowNumber = 10,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = "1990-01-01",
            Country = countryCode
        };

        var result = validator.Validate(row);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void ImportStatus_DefaultsToPending()
    {
        var job = new ImportJob();

        Assert.That(job.Status, Is.EqualTo(ImportStatus.Pending));
    }
}
