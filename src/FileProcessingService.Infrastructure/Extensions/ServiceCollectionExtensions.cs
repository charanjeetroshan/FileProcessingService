using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Exports;
using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Validation.Validators;
using FileProcessingService.Infrastructure.Csv;
using FileProcessingService.Infrastructure.Customers;
using FileProcessingService.Infrastructure.Exports;
using FileProcessingService.Infrastructure.FileStorage;
using FileProcessingService.Infrastructure.Imports;
using FileProcessingService.Infrastructure.Persistence;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FileProcessingService.Infrastructure.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FileProcessingDatabase")
            ?? throw new InvalidOperationException("Connection string 'FileProcessingDatabase' was not found.");

        services.AddDbContext<FileProcessingDbContext>(options => options.UseSqlServer(connectionString));

        services.AddOptionServices();

        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IFileHasher, Sha256FileHasher>();

        services.AddScoped<ICsvCustomerFileReader, CsvCustomerFileReader>();
        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IImportJobRepository, ImportJobRepository>();
        services.AddScoped<IImportErrorRepository, ImportErrorRepository>();
        services.AddScoped<IImportJobProcessor, ImportJobProcessor>();
        services.AddScoped<IValidator<CustomerImportRow>, CustomerImportRowValidator>();
        services.AddScoped<IExporter, NdJsonExporter>();
        services.AddScoped<IExporter, CsvExporter>();

        return services;
    }

    private static void AddOptionServices(this IServiceCollection services)
    {
        services.AddOptions<FileStorageOptions>()
            .BindConfiguration(FileStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<CsvOptions>()
            .BindConfiguration(CsvOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
    }
}
