using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Stellaegis.Blobs.FileSystem;
using Stellaegis.Primitives;
using Xunit;

namespace Stellaegis.Blobs.Tests;

/// <summary>
/// Unit tests for <see cref="FileSystemBlobStore"/>, each against a temporary root of its own: content round-trips,
/// an absent blob reads as null and an empty one as an empty stream, and spaces never share a directory.
/// </summary>
public sealed class FileSystemBlobStoreTests : IDisposable
{
    private static readonly Compartment Acme = Compartment.Parse("acme");
    private static readonly Compartment Globex = Compartment.Parse("globex");

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
        var location = new BlobLocation(Acme, "2026/06/08/a.txt");

        await WriteAsync(location, "hello");

        Assert.Equal("hello", await ReadAsync(location));
    }

    [Fact]
    public async Task Writing_overwrites_the_previous_blob()
    {
        var location = new BlobLocation(Acme, "a.txt");

        await WriteAsync(location, "a longer first version");
        await WriteAsync(location, "second");

        Assert.Equal("second", await ReadAsync(location));
    }

    [Fact]
    public async Task Failed_write_keeps_the_previous_blob_and_leaves_no_temporary_file()
    {
        var location = new BlobLocation(Acme, "a.txt");
        await WriteAsync(location, "previous");

        await Assert.ThrowsAsync<IOException>(() =>
            _store.WriteAsync(location, new FailingStream(), TestContext.Current.CancellationToken));

        Assert.Equal("previous", await ReadAsync(location));
        Assert.Equal(["a.txt"], Directory.GetFiles(Path.Combine(_root, "acme")).Select(Path.GetFileName));
    }

    [Fact]
    public async Task Planted_temporary_file_fails_the_write_and_stays_in_place()
    {
        var id = Guid.NewGuid();
        var pinned = new FileSystemBlobStore(
            Options.Create(new FileSystemBlobOptions { RootPath = _root }), NullLogger<FileSystemBlobStore>.Instance)
        { NewTemporaryId = () => id };

        var location = new BlobLocation(Acme, "a.txt");
        await WriteAsync(location, "previous");
        var planted = Path.Combine(_root, "acme", FileSystemBlobStore.TemporaryName(id));
        await File.WriteAllTextAsync(planted, "planted", TestContext.Current.CancellationToken);

        await Assert.ThrowsAsync<IOException>(() =>
            pinned.WriteAsync(location, new MemoryStream("new"u8.ToArray()), TestContext.Current.CancellationToken));

        Assert.Equal("previous", await ReadAsync(location));
        Assert.Equal("planted", await File.ReadAllTextAsync(planted, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Key_with_a_segment_of_the_maximum_length_is_written()
    {
        var location = new BlobLocation(Acme, new string('a', BlobLocation.MaxSegmentLength));

        await WriteAsync(location, "hello");

        Assert.Equal("hello", await ReadAsync(location));
    }

    [Fact]
    public async Task Absent_blob_reads_as_null()
    {
        var location = new BlobLocation(Acme, "missing.txt");

        Assert.Null(await _store.OpenReadAsync(location, TestContext.Current.CancellationToken));
        Assert.False(await _store.ExistsAsync(location, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Empty_blob_reads_as_an_empty_stream_not_as_null()
    {
        var location = new BlobLocation(Acme, "empty.txt");

        await _store.WriteAsync(location, Stream.Null, TestContext.Current.CancellationToken);

        await using var stream = await _store.OpenReadAsync(location, TestContext.Current.CancellationToken);
        Assert.NotNull(stream);
        Assert.Equal(0, stream.Length);
        Assert.True(await _store.ExistsAsync(location, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Delete_removes_the_blob_and_ignores_an_absent_one()
    {
        var location = new BlobLocation(Acme, "a.txt");
        await WriteAsync(location, "hello");

        await _store.DeleteAsync(location, TestContext.Current.CancellationToken);
        await _store.DeleteAsync(location, TestContext.Current.CancellationToken);

        Assert.False(await _store.ExistsAsync(location, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Same_key_is_isolated_by_compartment_and_shared_space()
    {
        var acme = new BlobLocation(Acme, "a.txt");
        var globex = new BlobLocation(Globex, "a.txt");
        var shared = new BlobLocation(Space.Shared, "a.txt");
        var namedShared = new BlobLocation(Compartment.Parse("shared"), "a.txt");

        await WriteAsync(acme, "acme");
        await WriteAsync(globex, "globex");
        await WriteAsync(shared, "shared");
        await WriteAsync(namedShared, "named shared");

        Assert.Equal("acme", await ReadAsync(acme));
        Assert.Equal("globex", await ReadAsync(globex));
        Assert.Equal("shared", await ReadAsync(shared));
        Assert.Equal("named shared", await ReadAsync(namedShared));
    }

    [Fact]
    public async Task Spaces_map_to_compartment_and_shared_directories()
    {
        await WriteAsync(new BlobLocation(Acme, "dir/a.txt"), "acme");
        await WriteAsync(new BlobLocation(Compartment.Parse("shared"), "a.txt"), "named shared");
        await WriteAsync(new BlobLocation(Space.Shared, "a.txt"), "shared");

        // Literal paths, not Space.ToSegment(): the layout is data already on disk, and a change of the projection
        // in Stellaegis.Primitives must fail here before it moves anything.
        Assert.True(File.Exists(Path.Combine(_root, "acme", "dir", "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "shared", "a.txt")));
        Assert.True(File.Exists(Path.Combine(_root, "_shared", "a.txt")));
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

    /// <summary>A source that yields a first chunk, then fails, as a connection cut during an upload does.</summary>
    private sealed class FailingStream : Stream
    {
        private bool _started;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_started)
                throw new IOException("The source failed during the copy.");

            _started = true;
            buffer[offset] = (byte)'x';
            return 1;
        }

        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
