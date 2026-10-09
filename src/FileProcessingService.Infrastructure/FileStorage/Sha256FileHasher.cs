using FileProcessingService.Application.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace FileProcessingService.Infrastructure.FileStorage;

public class Sha256FileHasher : IFileHasher
{
    public async Task<string> ComputeHashAsync(Stream content, CancellationToken cancellationToken = default)
    {
        if (content.Length == 0)
        {
            throw new ArgumentException("Content cannot be empty.", nameof(content));
        }

        using var sha256 = SHA256.Create();
        var hashBytes = await sha256.ComputeHashAsync(content, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    public async Task<string> ComputeHashAsync(string content, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(content))
        {
            throw new ArgumentException("Content cannot be null or whitespace.", nameof(content));
        }

        using var sha256 = SHA256.Create();
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        var hashBytes = await sha256.ComputeHashAsync(stream, cancellationToken);
        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}
