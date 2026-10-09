using FileProcessingService.Application.Contracts;
using FileProcessingService.Application.Validation.Validators;
using Microsoft.AspNetCore.Http;
using NSubstitute;

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
        var file = Substitute.For<IFormFile>();
        file.FileName.Returns(fileName);
        file.Length.Returns(length);
        return file;
    }

    [Test]
    public void Validate_WithNullFile_IsInvalidWithFileEmptyCode()
    {
        var request = new ImportJobRequest(null!);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorCode == "File.Empty"), Is.True);
    }

    [TestCase("package.zip", 1024)]
    [TestCase("package.ZIP", 1024)]
    [TestCase("package.zip", 50 * 1024 * 1024)]
    public void Validate_WithValidArchive_IsValid(string fileName, long length)
    {
        var file = CreateFormFile(fileName, length);
        var request = new ImportJobRequest(file);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.True);
    }

    [Test]
    public void Validate_WithFileExceedingMaxSize_IsInvalidWithTooLargeCode()
    {
        var file = CreateFormFile("package.zip", 50 * 1024 * 1024 + 1);
        var request = new ImportJobRequest(file);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorCode == "File.TooLarge"), Is.True);
    }

    [TestCase("customers.csv")]
    [TestCase("customers.CSV")]
    [TestCase("package.rar")]
    [TestCase("package.zip.txt")]
    [TestCase("package")]
    public void Validate_WithInvalidExtension_IsInvalidWithInvalidExtensionCode(string fileName)
    {
        var file = CreateFormFile(fileName, 1024);
        var request = new ImportJobRequest(file);

        var result = validator.Validate(request);

        Assert.That(result.IsValid, Is.False);
        Assert.That(result.Errors.Any(e => e.ErrorCode == "File.InvalidExtension"), Is.True);
    }
}
