using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Filesystem implementation of <see cref="IBlobStore"/> for development. Each blob space maps to a
/// directory under the configured root: <c>{root}/{holder}_{name}</c> for a holder, or
/// <c>{root}/_platform_{name}</c> for the platform's own space. A holder starts with a letter and a
/// holder or a space name never contains <c>_</c>, so no two locations share a directory and the
/// platform's leading underscore can never be produced by a holder.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    private const string PlatformContainerPrefix = "_platform_";

    private readonly string _root;
    private readonly ILogger<FileSystemBlobStore> _logger;

    /// <summary>Creates the store, validating that a root path is configured.</summary>
    public FileSystemBlobStore(IOptions<FileSystemBlobOptions> options, ILogger<FileSystemBlobStore> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        var root = options.Value.RootPath;
        if (string.IsNullOrWhiteSpace(root))
            throw new ArgumentException($"{nameof(FileSystemBlobOptions)}.{nameof(FileSystemBlobOptions.RootPath)} is required.", nameof(options));

        _root = Path.GetFullPath(root);
    }

    /// <inheritdoc />
    public Task<Stream> OpenReadAsync(BlobLocation location, CancellationToken cancellationToken = default)
    {
        var path = ResolvePath(location);
        if (!File.Exists(path))
            throw new BlobNotFoundException(location);

        Stream stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc />
    public async Task WriteAsync(BlobLocation location, Stream content, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        var path = ResolvePath(location);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        await using var file = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
        await content.CopyToAsync(file, cancellationToken).ConfigureAwait(false);

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

    /// <inheritdoc />
    /// <remarks>The filesystem adapter cannot pre-sign URLs — always returns <see langword="null"/> so the host streams.</remarks>
    public Task<Uri?> TryGetDownloadUrlAsync(BlobLocation location, TimeSpan validity, CancellationToken cancellationToken = default)
        => Task.FromResult<Uri?>(null);

    private static string Container(BlobLocation location)
        => location.IsPlatform ? $"{PlatformContainerPrefix}{location.Name}" : $"{location.Holder}_{location.Name}";

    private string ResolvePath(BlobLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);

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
