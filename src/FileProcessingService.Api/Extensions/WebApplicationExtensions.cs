using FileProcessingService.Api.Middlewares;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Serilog;

namespace FileProcessingService.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseWebApi(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthorization();

        app.MapControllers();

        app.UseMiddleware<RequestLoggingMiddleware>();

        app.UseExceptionHandler(_ => { });

        app.Lifetime.ApplicationStarted.Register(() => LogHostInformation(app));

        return app;
    }

    private static void LogHostInformation(WebApplication app)
    {
        Log.Information("{WebApi} started...", AppDomain.CurrentDomain.FriendlyName);
        Log.Information("Hosting environment: {Environment}", app.Environment.EnvironmentName);

        var server = app.Services.GetRequiredService<IServer>();
        var addresses = server.Features.Get<IServerAddressesFeature>()?.Addresses;

        if (addresses?.Count != 0)
        {
            var address = addresses?.ElementAt(0);
            Log.Information("Serving at: {Address}", address);
            Log.Information("Api docs at: {Address}", address + "/swagger");
        }
    }
}
