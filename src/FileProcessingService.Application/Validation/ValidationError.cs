namespace FileProcessingService.Application.Validation;

public record ValidationError(string ErrorCode, string Message, IReadOnlyList<ValidationErrorField>? Errors);