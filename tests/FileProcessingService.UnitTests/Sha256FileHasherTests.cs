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

    [Test]
    public async Task ComputeHashAsync_WithString_ReturnsKnownSha256()
    {
        var hash = await hasher.ComputeHashAsync("abc");

        Assert.That(hash, Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }

    [TestCase("CUSTOMERS.CSV:hash|")]
    [TestCase("客户 é 😀")]
    public async Task ComputeHashAsync_WithString_MatchesUtf8StreamHash(string content)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        Assert.That(await hasher.ComputeHashAsync(content), Is.EqualTo(await hasher.ComputeHashAsync(stream)));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t\r\n")]
    public void ComputeHashAsync_WithEmptyString_Throws(string? content)
    {
        Assert.ThrowsAsync<ArgumentException>(() => hasher.ComputeHashAsync(content!));
    }

    [Test]
    public void ComputeHashAsync_WithEmptyStream_Throws()
    {
        using var stream = new MemoryStream();

        Assert.ThrowsAsync<ArgumentException>(() => hasher.ComputeHashAsync(stream));
    }

    [Test]
    public void ComputeHashAsync_WhenCancelled_Throws()
    {
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        Assert.CatchAsync<OperationCanceledException>(() => hasher.ComputeHashAsync("content", cancellation.Token));
    }
}
