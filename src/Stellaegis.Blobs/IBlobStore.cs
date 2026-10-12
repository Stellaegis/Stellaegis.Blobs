namespace Stellaegis.Blobs;

/// <summary>
/// Stores and retrieves opaque binary content, isolated per tenant. The provider-agnostic
/// counterpart of <c>ISecretsProvider</c> for large binary payloads: one abstraction, swappable
/// adapters (filesystem, S3-compatible, Azure Blob) selected by configuration.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the vault, a blob store is a <b>library, not a hosted service</b>: bytes flow directly
/// between the consuming service and the storage backend — the remote endpoint <i>is</i> S3/Azure.
/// The credentials for a tenant's backend, however, are resolved like a tenant database connection
/// string (default installation binding, or a per-tenant vault override for BYOS).
/// </para>
/// <para>Implementations must never log blob content.</para>
/// </remarks>
public interface IBlobStore
{
    /// <summary>Opens a readable stream over the blob at <paramref name="location"/>.</summary>
    /// <exception cref="BlobNotFoundException">No blob exists at the location.</exception>
    Task<Stream> OpenReadAsync(BlobLocation location, CancellationToken cancellationToken = default);

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
