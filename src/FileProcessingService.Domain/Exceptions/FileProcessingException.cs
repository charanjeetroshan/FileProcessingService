using System.Net;

namespace FileProcessingService.Domain.Exceptions;

public class FileProcessingException : Exception
{
    public FileProcessingException(HttpStatusCode statusCode, string message) : base(message)
    {
        StatusCode = statusCode;
    }

    public FileProcessingException(HttpStatusCode statusCode, string message, object? data) : base(message)
    {
        StatusCode = statusCode;
        Data = data;
    }

    public HttpStatusCode StatusCode { get; set; }

    public new object? Data { get; set; }
}
