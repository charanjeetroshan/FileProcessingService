using FileProcessingService.Application.Contracts;
using FileProcessingService.Domain.Constants;
using FluentValidation;

namespace FileProcessingService.Application.Validation.Validators;

public class CreateImportJobValidator : AbstractValidator<ImportJobRequest>
{
    public CreateImportJobValidator()
    {
        RuleFor(x => x.File).NotEmpty()
            .WithMessage("A non-empty file must be provided.")
            .WithErrorCode("File.Empty");

        When(x => x.File is not null, () =>
        {
            RuleFor(x => x.File.FileName).Must(HaveValidExtension)
                .WithMessage(x => $"Unsupported file type '{Path.GetExtension(x.File.FileName)}'. Allowed extensions: '{string.Join(", ", FileConstants.AllowedExtensions)}'.")
                .WithErrorCode("File.InvalidExtension");

            RuleFor(x => x.File.Length).LessThanOrEqualTo(FileConstants.MaxFileSizeBytes)
                .WithMessage($"File exceeds the maximum allowed size of {FileConstants.MaxFileSizeBytes / (1024 * 1024)} MB.")
                .WithErrorCode("File.TooLarge");
        });
    }

    private static bool HaveValidExtension(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return FileConstants.AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }
}
