using FileProcessingService.Infrastructure.DependencyInjection;
using FileProcessingService.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) =>
    configuration.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ImportWorker>();

var host = builder.Build();
host.Run();
