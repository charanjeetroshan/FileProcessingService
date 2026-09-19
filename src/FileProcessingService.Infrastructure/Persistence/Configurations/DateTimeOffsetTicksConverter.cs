using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace FileProcessingService.Infrastructure.Persistence.Configurations;

/// <summary>
/// Converts <see cref="DateTimeOffset"/> values to their UTC tick count and back.
/// SQLite has no native DateTimeOffset type and cannot translate ORDER BY/comparisons
/// against it, so columns that are ordered or compared use this converter to store
/// a comparable primitive value while still round-tripping to DateTimeOffset in code.
/// </summary>
public class DateTimeOffsetTicksConverter()
    : ValueConverter<DateTimeOffset, long>(
        dateTimeOffset => dateTimeOffset.UtcTicks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));
