using FileProcessingService.Application.Imports;
using FileProcessingService.Domain.Entities;
using System.Diagnostics.CodeAnalysis;

namespace FileProcessingService.Application.Customers;

public static class CustomerRowMapper
{
    public static bool TryMap(
        CustomerImportRow row,
        Guid importId,
        [NotNullWhen(true)] out Customer? customer,
        [NotNullWhen(false)] out string? errorMessage)
    {
        if (!DateOnly.TryParse(row.DateOfBirth, out var dateOfBirth))
        {
            customer = null;
            errorMessage = $"'{row.DateOfBirth}' is not a valid date of birth.";
            return false;
        }

        customer = new Customer
        {
            Id = Guid.NewGuid(),
            ImportId = importId,
            FirstName = row.FirstName?.Trim() ?? string.Empty,
            LastName = row.LastName?.Trim() ?? string.Empty,
            Email = row.Email?.Trim() ?? string.Empty,
            DateOfBirth = dateOfBirth,
            Country = row.Country?.Trim() ?? string.Empty,
            CreatedAt = DateTimeOffset.UtcNow
        };
        errorMessage = null;
        return true;
    }
}
