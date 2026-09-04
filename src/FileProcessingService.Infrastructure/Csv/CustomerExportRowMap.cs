using CsvHelper.Configuration;
using FileProcessingService.Application.Exports;

namespace FileProcessingService.Infrastructure.Csv;

public sealed class CustomerExportRowMap : ClassMap<CustomerExportRow>
{
    public CustomerExportRowMap()
    {
        Map(row => row.Id).Name("Id");
        Map(row => row.ImportId).Name("ImportId");
        Map(row => row.FirstName).Name("FirstName");
        Map(row => row.LastName).Name("LastName");
        Map(row => row.Email).Name("Email");
        Map(row => row.DateOfBirth).Name("DateOfBirth");
        Map(row => row.Country).Name("Country");
    }
}
