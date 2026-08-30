using FileProcessingService.Application.Imports;
using FileProcessingService.Infrastructure.Csv;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using System.Text;

namespace FileProcessingService.UnitTests;

public class CsvCustomerFileReaderTests
{
    private static CsvCustomerFileReader CreateReader(string separator = ",")
    {
        var options = Options.Create(new CsvOptions { Separator = separator });
        return new CsvCustomerFileReader(options, NullLogger<CsvCustomerFileReader>.Instance);
    }

    private static Stream ToStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Test]
    public async Task ReadAsync_WithValidCsv_ReturnsMappedRowsWithRowNumbers()
    {
        var csv = "FirstName,LastName,Email,DateOfBirth,Country\n" +
                  "Jane,Doe,jane.doe@example.com,1990-01-01,US\n" +
                  "John,Smith,john.smith@example.com,1985-05-05,UK\n";

        var reader = CreateReader();
        using var stream = ToStream(csv);

        var rows = new List<CustomerImportRow>();
        await foreach (var row in reader.ReadAsync(stream))
        {
            rows.Add(row);
        }

        Assert.That(rows, Has.Count.EqualTo(2));
        Assert.That(rows[0].RowNumber, Is.EqualTo(2));
        Assert.That(rows[0].FirstName, Is.EqualTo("Jane"));
        Assert.That(rows[1].RowNumber, Is.EqualTo(3));
        Assert.That(rows[1].FirstName, Is.EqualTo("John"));
    }

    [Test]
    public async Task ReadAsync_WithOnlyHeader_ReturnsNoRows()
    {
        var csv = "FirstName,LastName,Email,DateOfBirth,Country\n";

        var reader = CreateReader();
        using var stream = ToStream(csv);

        var rows = new List<CustomerImportRow>();
        await foreach (var row in reader.ReadAsync(stream))
        {
            rows.Add(row);
        }

        Assert.That(rows, Is.Empty);
    }

    [Test]
    public async Task ReadAsync_WithCustomSeparator_ParsesCorrectly()
    {
        var csv = "FirstName;LastName;Email;DateOfBirth;Country\n" +
                  "Jane;Doe;jane.doe@example.com;1990-01-01;US\n";

        var reader = CreateReader(separator: ";");
        using var stream = ToStream(csv);

        var rows = new List<CustomerImportRow>();
        await foreach (var row in reader.ReadAsync(stream))
        {
            rows.Add(row);
        }

        Assert.That(rows, Has.Count.EqualTo(1));
        Assert.That(rows[0].Email, Is.EqualTo("jane.doe@example.com"));
    }

    [Test]
    public void ReadAsync_WhenCancelled_ThrowsOperationCanceledException()
    {
        var csv = "FirstName,LastName,Email,DateOfBirth,Country\n" +
                  "Jane,Doe,jane.doe@example.com,1990-01-01,US\n";

        var reader = CreateReader();
        using var stream = ToStream(csv);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        Assert.ThrowsAsync<OperationCanceledException>(async () =>
        {
            await foreach (var _ in reader.ReadAsync(stream, cts.Token))
            {
            }
        });
    }
}
