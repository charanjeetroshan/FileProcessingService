namespace FileProcessingService.Application.Abstractions;

public interface IFileHasher
{
    Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default);

    Task<string> ComputeHashAsync(string content, CancellationToken cancellationToken = default);
}
