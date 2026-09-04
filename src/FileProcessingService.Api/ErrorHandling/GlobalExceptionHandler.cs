using FileProcessingService.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FileProcessingService.Api.ErrorHandling;

public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken ct)
    {
        logger.LogError(
            "Unhandled exception occurred. TraceId: {TraceId} \n Exception: {@Exception}",
            httpContext.TraceIdentifier,
            exception);

        var problemDetails = CreateProblemDetails(httpContext, exception);

        httpContext.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;
        httpContext.Response.ContentType = "application/problem+json";

        await httpContext.Response.WriteAsJsonAsync(problemDetails, problemDetails.GetType(), ct);

        return true;
    }

    private static ProblemDetails CreateProblemDetails(HttpContext httpContext, Exception exception)
    {
        ProblemDetails problemDetails = exception switch
        {
            ValidationException validationException => new ValidationProblemDetails(
                validationException.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage).ToArray()))
            {
                Title = "Validation Error",
                Detail = "One or more validation errors occurred.",
                Status = StatusCodes.Status400BadRequest,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.1"
            },

            DbUpdateConcurrencyException => new ProblemDetails
            {
                Title = "A database concurrency error occurred.",
                Detail = "The request could not be completed due to a concurrency error.",
                Status = StatusCodes.Status409Conflict,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.5.10"
            },

            DbUpdateException => new ProblemDetails
            {
                Title = "A database error occurred.",
                Detail = "The request could not be completed due to a data persistence error.",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            },

            FileProcessingException ex => new ProblemDetails
            {
                Title = nameof(FileProcessingException),
                Detail = ex.Message,
                Status = (int)ex.StatusCode,
                Extensions = new Dictionary<string, object?> { { "data", ex.Data } }
            },

            _ => new ProblemDetails
            {
                Title = "An unexpected error occurred.",
                Detail = "The server encountered an error and could not process the request.",
                Status = StatusCodes.Status500InternalServerError,
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.1"
            }
        };

        problemDetails.Instance = $"{httpContext.Request.Method} '{httpContext.Request.Path}'";
        problemDetails.Extensions["traceId"] = httpContext.TraceIdentifier;

        return problemDetails;
    }
}