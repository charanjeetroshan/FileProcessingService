using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FileProcessingService.UnitTests;

/// <summary>
/// Creates an isolated, open-connection SQLite in-memory FileProcessingDbContext for repository tests.
/// The connection must be kept open for the lifetime of the context/test, and disposed afterwards.
/// </summary>
public static class SqliteDbContextFactory
{
    public static (FileProcessingDbContext DbContext, SqliteConnection Connection) Create()
    {
        var connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var options = new DbContextOptionsBuilder<FileProcessingDbContext>()
            .UseSqlite(connection)
            .Options;

        var dbContext = new FileProcessingDbContext(options);
        dbContext.Database.EnsureCreated();

        return (dbContext, connection);
    }
}
