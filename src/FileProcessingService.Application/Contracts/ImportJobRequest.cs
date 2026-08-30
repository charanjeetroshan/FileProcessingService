using Microsoft.AspNetCore.Http;

namespace FileProcessingService.Application.Contracts;

public record ImportJobRequest(IFormFile File);
