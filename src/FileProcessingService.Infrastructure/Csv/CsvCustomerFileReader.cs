using System.Globalization;
using System.Runtime.CompilerServices;
using CsvHelper;
using CsvHelper.Configuration;
using FileProcessingService.Application.Imports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FileProcessingService.Infrastructure.Csv;

public class CsvCustomerFileReader(IOptions<CsvOptions> options, ILogger<CsvCustomerFileReader> logger) : ICsvCustomerFileReader
{
    public async IAsyncEnumerable<CustomerImportRow> ReadAsync(
        Stream csvStream,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var streamReader = new StreamReader(csvStream);
        var configuration = new CsvConfiguration(CultureInfo.InvariantCulture)
        {
            Delimiter = options.Value.Separator
        };
        using var csvReader = new CsvReader(streamReader, configuration);
        csvReader.Context.RegisterClassMap<CustomerImportRowMap>();

        await csvReader.ReadAsync();
        csvReader.ReadHeader();

        logger.LogDebug("Starting CSV read with delimiter '{Delimiter}'", options.Value.Separator);

        long rowNumber = 1;
        while (await csvReader.ReadAsync())
        {
            cancellationToken.ThrowIfCancellationRequested();
            rowNumber++;

            var record = csvReader.GetRecord<CustomerImportRow>();
            yield return record with { RowNumber = rowNumber };
        }

        logger.LogDebug("Finished CSV read; {RowCount} data rows read", rowNumber - 1);
    }
}
