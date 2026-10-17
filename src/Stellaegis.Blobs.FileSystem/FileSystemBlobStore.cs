using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Filesystem implementation of <see cref="IBlobStore"/> for development. Each blob space maps to a
/// directory under the configured root: <c>{root}/{compartment}/{name}</c> for a compartment, or
/// <c>{root}/_shared/{name}</c> for the shared space. Everything a compartment holds therefore sits under
/// one directory. The first segment is <see cref="Space.ToSegment"/>, the projection every Stellaegis module
/// gives a space in an address: no compartment can produce the shared directory's leading underscore.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    private readonly string _root;
    private readonly ILogger<FileSystemBlobStore> _logger;

    /// <summary>Creates the store, validating that a root path is configured.</summary>
    public FileSystemBlobStore(IOptions<FileSystemBlobOptions> options, ILogger<FileSystemBlobStore> logger)
    {
        Guard.NotNull(options);
        _logger = Guard.NotNull(logger);

        var root = Guard.Satisfies(options.Value.RootPath, r => !string.IsNullOrWhiteSpace(r),
            $"{nameof(FileSystemBlobOptions)}.{nameof(FileSystemBlobOptions.RootPath)} is required.", nameof(options));

        _root = Path.GetFullPath(root);
    }

    /// <inheritdoc />
    public Task<Stream?> OpenReadAsync(BlobLocation location, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(location);
        if (!File.Exists(path))
            return Task.FromResult<Stream?>(null);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult<Stream?>(stream);
    }

    /// <inheritdoc />
    public async Task WriteAsync(BlobLocation location, Stream content, CancellationToken cancellationToken = default)
    {
        Guard.NotNull(content);

        var path = ResolvePath(location);
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);

        // Written beside the blob, then renamed over it: a rename within one directory is atomic, so a reader sees
        // the previous content or the new one in full, and a failed copy leaves the previous content untouched.
        // Opened outside the try: a temporary that already exists is refused, and is not ours to delete.
        var temporary = Path.Combine(directory, TemporaryName(NewTemporaryId()));
        var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        try
        {
            await using (file)
                await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

            File.Move(temporary, path, overwrite: true);
        }
        catch
        {
            File.Delete(temporary);
            throw;
        }

        _logger.LogDebug("Wrote blob to {Container}/{Key}", Container(location), location.Key);
    }

    /// <inheritdoc />
    public Task<bool> ExistsAsync(BlobLocation location, CancellationToken cancellationToken = default)
        => Task.FromResult(File.Exists(ResolvePath(location)));

    /// <inheritdoc />
    public Task DeleteAsync(BlobLocation location, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(location);
        if (File.Exists(path))
        {
            File.Delete(path);
            _logger.LogDebug("Deleted blob {Container}/{Key}", Container(location), location.Key);
        }

        return Task.CompletedTask;
    }

    /// <summary>Gives each write its temporary file. Replaced by the tests only.</summary>
    internal Func<Guid> NewTemporaryId { get; init; } = Guid.NewGuid;

    /// <summary>
    /// The name of a temporary file: no key can produce it, since a key is ASCII and <c>§</c> (U+00A7) is not, and
    /// its length is fixed, whatever the length of the key.
    /// </summary>
    internal static string TemporaryName(Guid id) => $"§{id:N}.tmp";

    private static string Container(BlobLocation location)
        => $"{location.Space.ToSegment()}/{location.Name}";

    private string ResolvePath(BlobLocation location)
    {
        Guard.NotNull(location);

        var containerRoot = Path.GetFullPath(Path.Combine(_root, Container(location)));
        var full = Path.GetFullPath(Path.Combine(containerRoot, Path.Combine(location.Key.Split('/'))));

        // Defence in depth: BlobLocation already rejects '..'/rooted keys, but never trust a path build.
        if (full != containerRoot &&
            !full.StartsWith(containerRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new ArgumentException($"Resolved blob path escapes its container: '{location.Key}'.", nameof(location));
        }

        return full;
    }
}
