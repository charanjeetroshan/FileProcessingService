using FileProcessingService.Application.Abstractions;

namespace FileProcessingService.Infrastructure.FileStorage;

public class Sha256FileHasher : IFileHasher
{
    public async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default)
    {
        using var sha256 = System.Security.Cryptography.SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
