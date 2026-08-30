using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Results;

namespace FileProcessingService.Application.Validation;

public sealed class ValidationResultFactory : IFluentValidationAutoValidationResultFactory
{
    public async Task<IActionResult?> CreateActionResult(
        ActionExecutingContext _,
        ValidationProblemDetails problem,
        IDictionary<IValidationContext, ValidationResult> __)
    {
        var errors = problem?.Errors.SelectMany(keyValuePair => keyValuePair.Value
            .Select(message => new ValidationErrorField(keyValuePair.Key, message))).ToList();

        var validationError = new ValidationError("ValidationError", "One or more validation errors occurred.", errors);

        return new BadRequestObjectResult(validationError);
    }
}
