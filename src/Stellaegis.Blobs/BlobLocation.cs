using System.Text.RegularExpressions;

namespace Stellaegis.Blobs;

/// <summary>
/// Addresses a blob by two stable, non-physical layers:
/// <list type="bullet">
///   <item><b>Where</b> — <see cref="Compartment"/> (the compartment the blob belongs to, or none for the
///   shared space) and <see cref="Name"/> (the blob space, chosen by the consumer). Together they select the
///   space an adapter stores the blob in.</item>
///   <item><b>What</b> — <see cref="Key"/>, the logical object path <i>within</i> that space.</item>
/// </list>
/// The physical provider, endpoint, container and credentials are never part of a location: they belong
/// to the <see cref="IBlobStore"/> implementation. This keeps stored keys stable across a backend migration
/// and free of any secret.
/// </summary>
/// <remarks>
/// <para>
/// Content that belongs to no compartment lives in the shared space, reached only through
/// <see cref="Shared"/>. It is a case of its own rather than a reserved compartment, so no compartment can
/// ever designate it, and a missing compartment is refused instead of falling into it.
/// </para>
/// <para>
/// A location only enforces the grammar its adapters need to store a blob without collision or path
/// traversal. Any further rule on a space name belongs to the consumer, which applies it before building
/// a location.
/// </para>
/// </remarks>
public sealed partial record BlobLocation
{
    /// <summary>
    /// The compartment the blob belongs to, or <see langword="null"/> for the shared space
    /// (<see cref="IsShared"/>).
    /// </summary>
    public Compartment? Compartment { get; }

    /// <summary>
    /// Blob space discriminant: a lowercase letter, followed by up to 62 lowercase letters, digits or hyphens.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Logical object key within the blob space (e.g. <c>invoice.scriban</c>
    /// or <c>2026/06/08/{id}.pdf</c>). Forward-slash separated, relative, never rooted.
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

    /// <summary>Creates a validated location in the space of <paramref name="compartment"/>.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="compartment"/>, <paramref name="name"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space name or the key is malformed, or the key escapes its space.</exception>
    public BlobLocation(Compartment compartment, string name, string key)
        : this(name, key)
        => Compartment = Guard.NotNull(compartment);

    private BlobLocation(string name, string key)
    {
        Guard.NotWhiteSpace(name);
        Guard.NotWhiteSpace(key);

        Name = Guard.Satisfies(name, n => NameRegex().IsMatch(n),
            $"'{name}' is not a valid blob space name.");
        Key = Guard.Satisfies(key, IsValidKey,
            $"'{key}' is not a valid blob key (relative, forward-slash separated, lowercase printable ASCII " +
            "without < > : \" | ? * \\, no empty segment, no segment ending with a dot or a space, " +
            "no Windows device name, at most 1024 characters and 255 per segment).");
    }

    /// <summary>Creates a location in the shared space, which belongs to no compartment.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> or <paramref name="key"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The space name or the key is malformed, or the key escapes its space.</exception>
    public static BlobLocation Shared(string name, string key) => new(name, key);

    /// <summary><see langword="true"/> when this location designates the shared space.</summary>
    public bool IsShared => Compartment is null;

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

    [GeneratedRegex("^[a-z][a-z0-9-]{0,62}$")]
    private static partial Regex NameRegex();
}
