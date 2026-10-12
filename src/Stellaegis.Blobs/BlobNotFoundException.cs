namespace Stellaegis.Blobs;

/// <summary>
/// Thrown by <see cref="IBlobStore.OpenReadAsync"/> when no blob exists at the requested location.
/// </summary>
public sealed class BlobNotFoundException : Exception
{
    /// <summary>Creates the exception for the given <paramref name="location"/>.</summary>
    public BlobNotFoundException(BlobLocation location)
        : base($"No blob found at {Describe(location)}.")
        => Location = location;

    /// <summary>The location that was not found.</summary>
    public BlobLocation Location { get; }

    private static string Describe(BlobLocation location)
    {
        ArgumentNullException.ThrowIfNull(location);
        var holder = location.IsPlatform ? "platform" : location.Holder;
        return $"{holder}/{location.Name}/{location.Key}";
    }
}
