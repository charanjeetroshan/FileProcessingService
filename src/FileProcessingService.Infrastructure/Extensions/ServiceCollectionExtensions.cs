using FileProcessingService.Application.Abstractions;
using FileProcessingService.Application.Cleanup;
using FileProcessingService.Application.Customers;
using FileProcessingService.Application.Exports;
using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Imports.Abstractions;
using FileProcessingService.Application.Validation.Validators;
using FileProcessingService.Infrastructure.Cleanup;
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

namespace FileProcessingService.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("FileProcessingDatabase")
            ?? throw new InvalidOperationException("Connection string 'FileProcessingDatabase' was not found.");

        services.AddDbContext<FileProcessingDbContext>(options => options.UseSqlServer(connectionString));

        services.AddOptions<CsvOptions>()
            .BindConfiguration(CsvOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddSingleton<IValidator<CustomerImportRow>, CustomerImportRowValidator>();

        services.AddSingleton<IFileStorageService, LocalFileStorageService>();
        services.AddSingleton<IFileHasher, Sha256FileHasher>();
        services.AddSingleton<ICompressionService, CompressionService>();
        services.AddSingleton<ICsvCustomerFileReader, CsvCustomerFileReader>();

        services.AddSingleton<IExporter, NdJsonExporter>();
        services.AddSingleton<IExporter, CsvExporter>();

        services.AddScoped<ICustomerRepository, CustomerRepository>();
        services.AddScoped<IImportJobRepository, ImportJobRepository>();
        services.AddScoped<IImportErrorRepository, ImportErrorRepository>();
        services.AddScoped<ICleanupJobRepository, CleanupJobRepository>();

        services.AddScoped<IImportJobProcessor, ImportJobProcessor>();

        return services;
    }
}
