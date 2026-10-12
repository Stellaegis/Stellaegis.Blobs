namespace Stellaegis.Blobs;

/// <summary>
/// Stores and retrieves opaque binary content, isolated per holder and blob space
/// (see <see cref="BlobLocation"/>). One abstraction, with the storage backend chosen by the adapter
/// registered.
/// </summary>
/// <remarks>
/// <para>
/// A blob store is a <b>library, not a hosted service</b>: bytes flow directly between the consuming
/// code and the storage backend.
/// </para>
/// <para>Implementations must never log blob content.</para>
/// </remarks>
public interface IBlobStore
{
    /// <summary>Opens a readable stream over the blob at <paramref name="location"/>.</summary>
    /// <returns>
    /// The content, or <see langword="null"/> when no blob exists at the location. An empty blob is not an
    /// absent one: it opens as a stream of length 0.
    /// </returns>
    Task<Stream?> OpenReadAsync(BlobLocation location, CancellationToken cancellationToken = default);

    /// <summary>Writes <paramref name="content"/> to <paramref name="location"/>, overwriting any existing blob.</summary>
    Task WriteAsync(BlobLocation location, Stream content, CancellationToken cancellationToken = default);

    /// <summary>Returns whether a blob exists at <paramref name="location"/>.</summary>
    Task<bool> ExistsAsync(BlobLocation location, CancellationToken cancellationToken = default);

    /// <summary>Deletes the blob at <paramref name="location"/>. No-op if it does not exist.</summary>
    Task DeleteAsync(BlobLocation location, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tries to produce a short-lived, directly downloadable URL for the blob at <paramref name="location"/>,
    /// valid for <paramref name="validity"/> — letting bytes flow straight between the browser and the storage
    /// backend (a pre-signed S3/Azure URL) instead of streaming through the host.
    /// </summary>
    /// <returns>
    /// The download URL, or <see langword="null"/> when the adapter cannot pre-sign (e.g. the filesystem
    /// adapter): the caller must then stream the content via <see cref="OpenReadAsync"/>.
    /// </returns>
    /// <remarks>
    /// A returned URL is not checked for existence — callers should only request one for a blob they have
    /// already authorized and whose metadata proves it exists.
    /// </remarks>
    Task<Uri?> TryGetDownloadUrlAsync(BlobLocation location, TimeSpan validity, CancellationToken cancellationToken = default);
}
