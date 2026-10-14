namespace Stellaegis.Blobs;

/// <summary>
/// Stores and retrieves opaque binary content, isolated per compartment and blob space
/// (see <see cref="BlobLocation"/>). One abstraction, with the storage backend chosen by the adapter
/// registered.
/// </summary>
/// <remarks>
/// <para>
/// A blob store is a <b>library, not a hosted service</b>: bytes flow directly between the consuming
/// code and the storage backend.
/// </para>
/// <para>
/// A blob store knows nothing of what it stores, nor of how the content reaches a user: serving it,
/// with its file name and type, is the job of the consumer, which reads it through
/// <see cref="OpenReadAsync"/>.
/// </para>
/// <para>
/// An implementation gives the shared space (<see cref="BlobLocation.IsShared"/>) a physical name that no
/// compartment can produce, so that no compartment ever reaches shared content.
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
}
