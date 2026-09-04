using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Imports;

namespace FileProcessingService.UnitTests;

public class CustomerRowMapperTests
{
    [Test]
    public void TryMap_WithValidRow_ReturnsTrueAndMapsFields()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 1,
            FirstName = " Jane ",
            LastName = " Doe ",
            Email = " jane.doe@example.com ",
            DateOfBirth = "1990-01-01",
            Country = " USA "
        };

        Guid randomImportId = Guid.NewGuid();

        var mapped = CustomerRowMapper.TryMap(row, randomImportId, out var customer, out var errorMessage);

        Assert.That(mapped, Is.True);
        Assert.That(errorMessage, Is.Null);
        Assert.That(customer, Is.Not.Null);
        Assert.That(customer.ImportId, Is.EqualTo(randomImportId));
        Assert.That(customer.FirstName, Is.EqualTo("Jane"));
        Assert.That(customer.LastName, Is.EqualTo("Doe"));
        Assert.That(customer.Email, Is.EqualTo("jane.doe@example.com"));
        Assert.That(customer.Country, Is.EqualTo("USA"));
        Assert.That(customer.DateOfBirth, Is.EqualTo(new DateOnly(1990, 1, 1)));
    }

    [Test]
    public void TryMap_WithInvalidDateOfBirth_ReturnsFalse()
    {
        var row = new CustomerImportRow
        {
            RowNumber = 2,
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = "not-a-date",
            Country = "USA"
        };

        var mapped = CustomerRowMapper.TryMap(row, Guid.NewGuid(), out var customer, out var errorMessage);

        Assert.That(mapped, Is.False);
        Assert.That(customer, Is.Null);
        Assert.That(errorMessage, Is.Not.Null.And.Contains("not-a-date"));
    }
}
