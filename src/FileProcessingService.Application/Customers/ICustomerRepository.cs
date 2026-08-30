using FileProcessingService.Domain.Entities;

namespace FileProcessingService.Application.Customers;

public interface ICustomerRepository
{
    Task<Customer?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task AddAsync(Customer customer, CancellationToken cancellationToken = default);
}
