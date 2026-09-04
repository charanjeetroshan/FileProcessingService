using Microsoft.AspNetCore.Mvc.Controllers;
using System.Diagnostics;

namespace FileProcessingService.Api.Middlewares;

public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var descriptor = context.GetEndpoint()?.Metadata.GetMetadata<ControllerActionDescriptor>();

        if (descriptor is null)
        {
            await next(context);
            return;
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            logger.LogInformation(
                "Starting HTTP {Method} {Controller}.{Action} for {Path} TraceId={TraceId}",
                context.Request.Method,
                descriptor.ControllerName,
                descriptor.ActionName,
                context.Request.Path,
                context.TraceIdentifier
            );

            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            logger.LogInformation(
                "Finished HTTP {Method} {Controller}.{Action} with {StatusCode} in {ElapsedMs} ms TraceId={TraceId}",
                context.Request.Method,
                descriptor.ControllerName,
                descriptor.ActionName,
                context.Response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                context.TraceIdentifier
            );
        }
    }
}