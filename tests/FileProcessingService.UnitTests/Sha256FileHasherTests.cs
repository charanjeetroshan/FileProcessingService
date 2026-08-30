using System.Text;
using FileProcessingService.Infrastructure.FileStorage;

namespace FileProcessingService.UnitTests;

public class Sha256FileHasherTests
{
    private Sha256FileHasher hasher = null!;

    [SetUp]
    public void Setup()
    {
        hasher = new Sha256FileHasher();
    }

    [Test]
    public async Task ComputeHashAsync_WithSameContent_ProducesSameHash()
    {
        var content = "hello,world\n1,2\n"u8.ToArray();

        using var stream1 = new MemoryStream(content);
        using var stream2 = new MemoryStream(content);

        var hash1 = await hasher.ComputeHashAsync(stream1);
        var hash2 = await hasher.ComputeHashAsync(stream2);

        Assert.That(hash1, Is.EqualTo(hash2));
    }

    [Test]
    public async Task ComputeHashAsync_WithDifferentContent_ProducesDifferentHash()
    {
        using var stream1 = new MemoryStream(Encoding.UTF8.GetBytes("content-a"));
        using var stream2 = new MemoryStream(Encoding.UTF8.GetBytes("content-b"));

        var hash1 = await hasher.ComputeHashAsync(stream1);
        var hash2 = await hasher.ComputeHashAsync(stream2);

        Assert.That(hash1, Is.Not.EqualTo(hash2));
    }

    [Test]
    public async Task ComputeHashAsync_ReturnsLowercaseHexString()
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes("sample"));

        var hash = await hasher.ComputeHashAsync(stream);

        Assert.That(hash, Is.EqualTo(hash.ToLowerInvariant()));
        Assert.That(hash, Has.Length.EqualTo(64));
    }
}
