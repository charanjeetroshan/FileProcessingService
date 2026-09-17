using FileProcessingService.Infrastructure.Extensions;
using FileProcessingService.Worker;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, configuration) =>
    configuration.ReadFrom.Configuration(builder.Configuration));

builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddHostedService<ImportWorker>();
builder.Services.AddHostedService<FileWatcherService>();
builder.Services.AddHostedService<CleanupWorker>();

var host = builder.Build();
host.Run();
