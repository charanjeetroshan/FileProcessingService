using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Validation.Validators;
using Microsoft.AspNetCore.Http;
using Moq;

namespace FileProcessingService.UnitTests;

public class CreateImportJobValidatorTests
{
    private CreateImportJobValidator validator = null!;

    [SetUp]
    public void Setup()
    {
        validator = new CreateImportJobValidator();
    }

    private static IFormFile CreateFormFile(string fileName, long length)
    {
        var mock = new Mock<IFormFile>();
        mock.Setup(f => f.FileName).Returns(fileName);
        mock.Setup(f => f.Length).Returns(length);
        return mock.Object;
    }

    [Test]
    public void Validate_WithNullFile_IsInvalidWithFileEmptyCode()
    {
        var request = new ImportJobRequest(null!);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorCode == "File.Empty"), Is.True);
    }

    [Test]
    public void Validate_WithValidCsvFile_IsValid()
    {
        var file = CreateFormFile("customers.csv", 1024);
        var request = new ImportJobRequest(file);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_WithFileExceedingMaxSize_IsInvalidWithTooLargeCode()
    {
        var file = CreateFormFile("customers.csv", 51 * 1024 * 1024);
        var request = new ImportJobRequest(file);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorCode == "File.TooLarge"), Is.True);
    }

    [Test]
    public void Validate_WithInvalidExtension_IsInvalidWithInvalidExtensionCode()
    {
        var file = CreateFormFile("customers.txt", 1024);
        var request = new ImportJobRequest(file);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorCode == "File.InvalidExtension"), Is.True);
    }
}
