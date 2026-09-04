using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Customers;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;

namespace FileProcessingService.UnitTests;

public class CustomerRepositoryTests
{
    private FileProcessingDbContext dbContext = null!;
    private SqliteConnection connection = null!;
    private CustomerRepository repository = null!;

    [SetUp]
    public void Setup()
    {
        (dbContext, connection) = SqliteDbContextFactory.Create();
        repository = new CustomerRepository(dbContext);
    }

    [TearDown]
    public void TearDown()
    {
        dbContext.Dispose();
        connection.Dispose();
    }

    [Test]
    public async Task AddAsync_PersistsCustomer()
    {
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Country = "US",
            CreatedAt = DateTimeOffset.UtcNow
        };

        await repository.AddAsync(customer);

        var stored = await dbContext.Customers.FindAsync(customer.Id);
        Assert.That(stored, Is.Not.Null);
        Assert.That(stored!.Email, Is.EqualTo("jane.doe@example.com"));
    }

    [Test]
    public async Task GetByEmailAsync_WithExistingEmail_ReturnsCustomer()
    {
        var customer = new Customer
        {
            FirstName = "Jane",
            LastName = "Doe",
            Email = "jane.doe@example.com",
            DateOfBirth = new DateOnly(1990, 1, 1),
            Country = "US",
            CreatedAt = DateTimeOffset.UtcNow
        };
        await repository.AddAsync(customer);

        var found = await repository.GetByEmailAsync("jane.doe@example.com");

        Assert.That(found, Is.Not.Null);
        Assert.That(found!.Id, Is.EqualTo(customer.Id));
    }

    [Test]
    public async Task GetByEmailAsync_WithNonExistentEmail_ReturnsNull()
    {
        var found = await repository.GetByEmailAsync("missing@example.com");

        Assert.That(found, Is.Null);
    }
}
