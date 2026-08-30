using CsvHelper.Configuration;
using FileProcessingService.Application.Imports;

namespace FileProcessingService.Infrastructure.Csv;

public sealed class CustomerImportRowMap : ClassMap<CustomerImportRow>
{
    public CustomerImportRowMap()
    {
        Map(row => row.FirstName).Name("FirstName");
        Map(row => row.LastName).Name("LastName");
        Map(row => row.Email).Name("Email");
        Map(row => row.DateOfBirth).Name("DateOfBirth");
        Map(row => row.Country).Name("Country");
    }
}
