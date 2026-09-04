using FileProcessingService.Application.Customers;
using FileProcessingService.Domain.Entities;
using FileProcessingService.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Runtime.CompilerServices;

namespace FileProcessingService.Infrastructure.Customers;

public class CustomerRepository(FileProcessingDbContext dbContext) : ICustomerRepository
{
    public async Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await dbContext.Customers.FirstOrDefaultAsync(customer => customer.Email == email, cancellationToken);
    }

    public async Task AddAsync(Customer customer, CancellationToken cancellationToken = default)
    {
        await dbContext.Customers.AddAsync(customer, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async IAsyncEnumerable<Customer> GetByImportIdAsync(Guid importId, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var customers = dbContext.Customers.AsNoTracking()
            .Where(customer => customer.ImportId == importId)
            .AsAsyncEnumerable().WithCancellation(cancellationToken);

        await foreach (var customer in customers)
        {
            yield return customer;
        }
    }
}
