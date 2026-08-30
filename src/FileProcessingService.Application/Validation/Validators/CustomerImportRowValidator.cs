using FileProcessingService.Application.Imports;
using FluentValidation;
using System.Globalization;

namespace FileProcessingService.Application.Validation.Validators;

public class CustomerImportRowValidator : AbstractValidator<CustomerImportRow>
{
    private static readonly HashSet<string> ValidCountryCodes = BuildValidCountryCodes();

    public CustomerImportRowValidator()
    {
        RuleFor(row => row.FirstName)
            .NotEmpty().WithMessage("{PropertyName} is required.").WithErrorCode("EmptyValue")
            .MaximumLength(100).WithMessage("{PropertyName} cannot exceed {MaxLength} characters.").WithErrorCode("MaximumLength");

        RuleFor(row => row.LastName)
            .NotEmpty().WithMessage("{PropertyName} is required.").WithErrorCode("EmptyValue")
            .MaximumLength(100).WithMessage("{PropertyName} cannot exceed {MaxLength} characters.").WithErrorCode("MaximumLength");

        RuleFor(row => row.Email)
            .NotEmpty().WithMessage("{PropertyName} is required.").WithErrorCode("EmptyValue")
            .MaximumLength(320).WithMessage("{PropertyName} cannot exceed {MaxLength} characters.").WithErrorCode("MaximumLength")
            .EmailAddress().WithMessage("{PropertyName} must be a valid email address.").WithErrorCode("InvalidEmailAddress");

        RuleFor(row => row.DateOfBirth)
            .NotEmpty().WithMessage("{PropertyName} is required.").WithErrorCode("EmptyValue")
            .Must(BeAValidDate)
                .WithMessage("'{PropertyName}' must be a valid date.").WithErrorCode("InvalidDate")
            .DependentRules(() =>
            {
                RuleFor(row => row.DateOfBirth)
                    .Must(NotBeInTheFuture)
                        .WithMessage("'{PropertyName}' cannot be in the future.").WithErrorCode("DateInFuture")
                    .Must(BeAtLeast18YearsOld)
                        .WithMessage("Customer must be at least 18 years old.").WithErrorCode("CustomerUnder18");
            });

        RuleFor(row => row.Country)
            .NotEmpty().WithMessage("{PropertyName} is required.").WithErrorCode("EmptyValue")
            .Must(BeAValidCountryCode)
                .WithMessage("'{PropertyName}' must be a valid ISO 3166-1 alpha-2 country code.").WithErrorCode("InvalidCountryCode");
    }

    private static bool BeAValidDate(string? dateOfBirth)
        => DateOnly.TryParse(dateOfBirth, CultureInfo.InvariantCulture, DateTimeStyles.None, out _);

    private static bool NotBeInTheFuture(string? dateOfBirth)
        => !DateOnly.TryParse(dateOfBirth, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
           || date <= DateOnly.FromDateTime(DateTime.UtcNow);

    private static bool BeAtLeast18YearsOld(string? dateOfBirth)
    {
        if (!DateOnly.TryParse(dateOfBirth, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return true;
        }

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var age = today.Year - date.Year;
        if (date > today.AddYears(-age))
        {
            age--;
        }

        return age >= 18;
    }

    private static bool BeAValidCountryCode(string? country)
        => !string.IsNullOrWhiteSpace(country) && ValidCountryCodes.Contains(country.Trim().ToUpperInvariant());

    private static HashSet<string> BuildValidCountryCodes()
    {
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.SpecificCultures))
        {
            try
            {
                var region = new RegionInfo(culture.Name);
                codes.Add(region.TwoLetterISORegionName);
            }
            catch (ArgumentException)
            {
                // Culture has no associated region; ignore.
            }
        }

        return codes;
    }
}
