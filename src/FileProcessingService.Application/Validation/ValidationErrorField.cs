namespace FileProcessingService.Application.Validation;

public sealed record ValidationErrorField(string PropertyName, string ErrorMessage);