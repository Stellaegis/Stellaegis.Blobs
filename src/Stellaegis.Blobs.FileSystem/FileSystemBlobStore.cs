using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Filesystem implementation of <see cref="IBlobStore"/> for development. Each blob space maps to a
/// directory under the configured root: <c>{root}/{compartment}_{name}</c> for a compartment, or
/// <c>{root}/_shared_{name}</c> for the shared space. A compartment starts with a letter and neither a
/// compartment nor a space name contains <c>_</c>, so no two locations share a directory and no
/// compartment can produce the shared space's leading underscore.
/// </summary>
public sealed class FileSystemBlobStore : IBlobStore
{
    private const string SharedContainerPrefix = "_shared_";

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

    private static string Container(BlobLocation location)
        => location.Compartment is { } compartment
            ? $"{compartment.Value}_{location.Name}"
            : $"{SharedContainerPrefix}{location.Name}";

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
