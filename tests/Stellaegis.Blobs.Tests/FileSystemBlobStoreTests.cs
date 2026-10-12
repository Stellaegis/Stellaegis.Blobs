using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stellaegis.Blobs.FileSystem;
using Xunit;

namespace Stellaegis.Blobs.Tests;

/// <summary>
/// Unit tests for <see cref="FileSystemBlobStore"/>, each against a temporary root of its own: content round-trips,
/// an absent blob reads as null and an empty one as an empty stream, and spaces never share a directory.
/// </summary>
public sealed class FileSystemBlobStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "stellaegis-blobs-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemBlobStore _store;

    public FileSystemBlobStoreTests()
        => _store = CreateStore(_root);

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, recursive: true);
    }

    [Fact]
    public async Task Written_content_reads_back()
    {
        var location = new BlobLocation("acme", "files", "2026/06/08/a.txt");

        await WriteAsync(location, "hello");

        Assert.Equal("hello", await ReadAsync(location));
    }

    [Fact]
    public async Task Writing_overwrites_the_previous_blob()
    {
        var location = new BlobLocation("acme", "files", "a.txt");

        await WriteAsync(location, "a longer first version");
        await WriteAsync(location, "second");

        Assert.Equal("second", await ReadAsync(location));
    }

    [Fact]
    public async Task Absent_blob_reads_as_null()
    {
        var location = new BlobLocation("acme", "files", "missing.txt");

        Assert.Null(await _store.OpenReadAsync(location, TestContext.Current.CancellationToken));
        Assert.False(await _store.ExistsAsync(location, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Empty_blob_reads_as_an_empty_stream_not_as_null()
    {
        var location = new BlobLocation("acme", "files", "empty.txt");

        await _store.WriteAsync(location, Stream.Null, TestContext.Current.CancellationToken);

        await using var stream = await _store.OpenReadAsync(location, TestContext.Current.CancellationToken);
        Assert.NotNull(stream);
        Assert.Equal(0, stream.Length);
        Assert.True(await _store.ExistsAsync(location, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_removes_the_blob_and_ignores_an_absent_one()
    {
        var location = new BlobLocation("acme", "files", "a.txt");
        await WriteAsync(location, "hello");

        await _store.DeleteAsync(location, TestContext.Current.CancellationToken);
        await _store.DeleteAsync(location, TestContext.Current.CancellationToken);

        Assert.False(await _store.ExistsAsync(location, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Same_key_is_isolated_by_holder_platform_and_space()
    {
        var acme = new BlobLocation("acme", "files", "a.txt");
        var globex = new BlobLocation("globex", "files", "a.txt");
        var platform = BlobLocation.ForPlatform("files", "a.txt");
        var otherSpace = new BlobLocation("acme", "archive", "a.txt");

        await WriteAsync(acme, "acme");
        await WriteAsync(globex, "globex");
        await WriteAsync(platform, "platform");
        await WriteAsync(otherSpace, "archive");

        Assert.Equal("acme", await ReadAsync(acme));
        Assert.Equal("globex", await ReadAsync(globex));
        Assert.Equal("platform", await ReadAsync(platform));
        Assert.Equal("archive", await ReadAsync(otherSpace));
    }

    [Fact]
    public async Task Spaces_map_to_holder_and_platform_directories()
    {
        await WriteAsync(new BlobLocation("acme", "files", "dir/a.txt"), "acme");
        await WriteAsync(BlobLocation.ForPlatform("files", "a.txt"), "platform");

        Assert.True(File.Exists(Path.Combine(_root, "acme_files", "dir", "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "_platform_files", "a.txt")));
    }

    [Fact]
    public async Task Download_url_is_never_offered()
    {
        var location = new BlobLocation("acme", "files", "a.txt");
        await WriteAsync(location, "hello");

        Assert.Null(await _store.TryGetDownloadUrlAsync(location, TimeSpan.FromMinutes(5), TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void Missing_root_is_refused(string root)
    {
        var exception = Assert.Throws<ArgumentException>(() => CreateStore(root));

        Assert.Equal("options", exception.ParamName);
    }

    private static FileSystemBlobStore CreateStore(string root)
        => new(Options.Create(new FileSystemBlobOptions { RootPath = root }), NullLogger<FileSystemBlobStore>.Instance);

    private Task WriteAsync(BlobLocation location, string content)
        => _store.WriteAsync(location, new MemoryStream(Encoding.UTF8.GetBytes(content)), TestContext.Current.CancellationToken);

    private async Task<string> ReadAsync(BlobLocation location)
    {
        await using var stream = await _store.OpenReadAsync(location, TestContext.Current.CancellationToken);
        Assert.NotNull(stream);
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
    }
}
