using FileProcessingService.Application.Configuration;
using FileProcessingService.Application.Exports;
using FileProcessingService.Application.Imports;
using FileProcessingService.Application.Validation;
using FluentValidation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;
using System.Reflection;

namespace FileProcessingService.Application.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddValidatorsFromAssembly(Assembly.GetExecutingAssembly());

        services.AddFluentValidationAutoValidation(validationConfig =>
        {
            validationConfig.DisableBuiltInModelValidation = true;
            validationConfig.EnablePathBindingSourceAutomaticValidation = true;

            validationConfig.OverrideDefaultResultFactoryWith<ValidationResultFactory>();
        });

        services.AddOptions<FileStorageOptions>()
            .BindConfiguration(FileStorageOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddScoped<IImportUploadService, ImportUploadService>();
        services.AddScoped<ICustomerExportService, CustomerExportService>();

        return services;
    }
}
