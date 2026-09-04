using FileProcessingService.Api.Extensions;
using FileProcessingService.Application.Extensions;
using FileProcessingService.Infrastructure.DependencyInjection;
using Serilog;

try
{
    var builder = WebApplication.CreateBuilder(args);

    // Configure Serilog first
    builder.Host.UseSerilog((context, services, configuration) => configuration.ReadFrom.Configuration(context.Configuration));

    builder.Services.AddWebApi();
    builder.Services.AddApplication();
    builder.Services.AddInfrastructure(builder.Configuration);

    var app = builder.Build();

    app.UseWebApi();

    app.Run();
}
catch (Exception exception)
{
    Log.Fatal("An exception occurred while starting the application {Error}", exception.Message);
    throw;
}
finally
{
    Log.CloseAndFlush();
}
