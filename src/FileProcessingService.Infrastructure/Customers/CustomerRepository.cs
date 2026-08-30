using FileProcessingService.Application.Customers;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FileProcessingService.Infrastructure.Customers;

public class CustomerRepository(FileProcessingDbContext dbContext, ILogger<CustomerRepository> logger) : ICustomerRepository
{
    public Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
        => dbContext.Customers.FirstOrDefaultAsync(customer => customer.Email == email, cancellationToken);

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogDebug("Added customer {CustomerId} with email {Email}", customer.Id, customer.Email);
    }
}
