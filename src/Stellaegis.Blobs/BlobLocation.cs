namespace Stellaegis.Blobs;

/// <summary>
/// Addresses a blob by two stable, non-physical layers:
/// <list type="bullet">
///   <item><b>Where</b> — <see cref="Space"/>, the compartment the blob belongs to, or the shared space.</item>
///   <item><b>What</b> — <see cref="Key"/>, the logical object path <i>within</i> that space.</item>
/// </list>
/// The physical provider, endpoint, container and credentials are never part of a location: they belong
/// to the <see cref="IBlobStore"/> implementation. This keeps stored keys stable across a backend migration
/// and free of any secret.
/// </summary>
/// <remarks>
/// <para>
/// A <see cref="Compartment"/> converts to its <see cref="Primitives.Space"/>, so a location in a compartment
/// is built from the compartment itself. Content that belongs to no compartment is built from
/// <see cref="Primitives.Space.Shared"/>, which no compartment can designate. A missing space is refused
/// instead of falling into the shared one: a <see langword="null"/> compartment converts to a
/// <see langword="null"/> space, never to the shared space.
/// </para>
/// <para>
/// A location only enforces the grammar its adapters need to store a blob without collision or path
/// traversal. How a consumer organizes its keys belongs to it: a first segment per kind of content, such as
/// <c>invoices/</c> or <c>trash/</c>, or a store of its own for each kind, which may then sit on a storage of
/// its own.
/// </para>
/// </remarks>
public sealed record BlobLocation
{
    /// <summary>
    /// The space the blob belongs to: a compartment, or <see cref="Primitives.Space.Shared"/>.
    /// </summary>
    public Space Space { get; }

    /// <summary>
    /// Logical object key within the space (e.g. <c>templates/invoice.scriban</c>
    /// or <c>invoices/2026/06/08/{id}.pdf</c>). Forward-slash separated, relative, never rooted.
    /// </summary>
    /// <remarks>
    /// A key is accepted only if every supported backend can store it unchanged: the file systems of Windows
    /// and Linux, and object storages such as S3 or Azure Blob. It is made of lowercase printable ASCII, at
    /// most <see cref="MaxKeyLength"/> characters, in segments of at most <see cref="MaxSegmentLength"/>
    /// characters. It contains none of <c>&lt; &gt; : " | ? * \</c>, no segment is empty or ends with a dot
    /// or a space, and no segment is a name Windows reserves for a device (<c>con</c>, <c>nul</c>,
    /// <c>com1</c>…, with or without an extension). A name chosen by a user, with its case, accents or
    /// spaces, is metadata to keep beside the blob, not a key.
    /// </remarks>
    public string Key { get; }

    /// <summary>The maximum length of a <see cref="Key"/>, in characters (one byte each, being ASCII).</summary>
    public const int MaxKeyLength = 1024;

    /// <summary>The maximum length of one segment of a <see cref="Key"/>, in characters.</summary>
    public const int MaxSegmentLength = 255;

    /// <summary>Creates a validated location in <paramref name="space"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="space"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The key is malformed, or escapes its space.</exception>
    public BlobLocation(Space space, string key)
    {
        Space = Guard.NotNull(space);
        Guard.NotWhiteSpace(key);

        Key = Guard.Satisfies(key, IsValidKey,
            $"'{key}' is not a valid blob key (relative, forward-slash separated, lowercase printable ASCII " +
            "without < > : \" | ? * \\, no empty segment, no segment ending with a dot or a space, " +
            "no Windows device name, at most 1024 characters and 255 per segment).");
    }

    /// <summary>
    /// Accepts a key every supported backend stores unchanged. Rooted paths, backslashes and <c>.</c> or
    /// <c>..</c> segments are refused among the rest, so a key can never escape the container an adapter
    /// resolves it into (defence in depth, independent of the backend).
    /// </summary>
    private static bool IsValidKey(string key)
    {
        if (key.Length > MaxKeyLength) return false;

        foreach (var c in key)
        {
            if (c is < ' ' or > '~' or (>= 'A' and <= 'Z') or '<' or '>' or ':' or '"' or '|' or '?' or '*' or '\\')
                return false;
        }

        foreach (var segment in key.Split('/'))
        {
            if (segment.Length is 0 or > MaxSegmentLength) return false;
            if (segment[^1] is '.' or ' ') return false;
            if (IsWindowsDeviceName(segment)) return false;
        }

        return true;
    }

    /// <summary>
    /// Windows reserves these names for devices, extension or not: <c>nul.txt</c> opens the null device.
    /// </summary>
    private static bool IsWindowsDeviceName(string segment)
    {
        var dot = segment.IndexOf('.', StringComparison.Ordinal);
        var stem = (dot < 0 ? segment : segment[..dot]).TrimEnd(' ');

        return stem is "con" or "prn" or "aux" or "nul"
            || (stem.Length == 4 && (stem.StartsWith("com", StringComparison.Ordinal) || stem.StartsWith("lpt", StringComparison.Ordinal))
                && stem[3] is >= '0' and <= '9');
    }
}
