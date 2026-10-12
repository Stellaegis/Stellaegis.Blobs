using System.Text.RegularExpressions;

namespace Stellaegis.Blobs;

/// <summary>
/// Addresses a blob by two stable, non-physical layers:
/// <list type="bullet">
///   <item><b>Where</b> — <see cref="Holder"/> (the party the blob belongs to, or <see cref="Platform"/>)
///   and <see cref="Name"/> (the blob space, chosen by the consumer). Together they select the space
///   an adapter stores the blob in.</item>
///   <item><b>What</b> — <see cref="Key"/>, the logical object path <i>within</i> that space.</item>
/// </list>
/// The physical provider, endpoint, container and credentials are never part of a location: they belong
/// to the <see cref="IBlobStore"/> implementation. This keeps stored keys stable across a backend migration
/// and free of any secret.
/// </summary>
/// <remarks>
/// A location only enforces the grammar its adapters need to store a blob without collision or path
/// traversal. Any further rule on a holder or a space name — a business format, reserved values — belongs
/// to the consumer, which applies it before building a location.
/// </remarks>
public sealed partial record BlobLocation
{
    /// <summary>
    /// The <see cref="Holder"/> value designating the platform itself rather than one of its holders.
    /// It lies outside the holder grammar, so it never collides with a real holder.
    /// </summary>
    public const string Platform = "";

    /// <summary>
    /// The holder the blob belongs to, or <see cref="Platform"/> (empty string) for content owned by
    /// the platform. A holder starts with a lowercase letter, followed by 2 to 62 lowercase letters,
    /// digits or single hyphens.
    /// </summary>
    public string Holder { get; }

    /// <summary>Blob space discriminant: a lowercase letter, then lowercase letters, digits or hyphens.</summary>
    public string Name { get; }

    /// <summary>
    /// Logical object key within the holder's blob space (e.g. <c>invoice.scriban</c>
    /// or <c>2026/06/08/{id}.pdf</c>). Forward-slash separated, relative, never rooted.
    /// </summary>
    public string Key { get; }

    /// <summary>Creates a validated blob location.</summary>
    /// <exception cref="ArgumentException">A segment is empty, malformed, or the key escapes its space.</exception>
    public BlobLocation(string holder, string name, string key)
    {
        Guard.NotNull(holder);
        Guard.NotWhiteSpace(name);
        Guard.NotWhiteSpace(key);

        Holder = Guard.Satisfies(holder, IsValidHolder,
            $"'{holder}' is not a valid holder (or the platform value '').");
        Name = Guard.Satisfies(name, n => NameRegex().IsMatch(n),
            $"'{name}' is not a valid blob space name.");
        Key = Guard.Satisfies(key, IsSafeKey,
            $"'{key}' is not a valid blob key (must be relative, forward-slash separated, no '..').");
    }

    /// <summary>Creates a location in the platform's own space (<see cref="Platform"/>).</summary>
    public static BlobLocation ForPlatform(string name, string key) => new(Platform, name, key);

    /// <summary><see langword="true"/> when this location designates the platform's own space.</summary>
    public bool IsPlatform => Holder.Length == 0;

    /// <summary>Accepts <see cref="Platform"/> or a holder matching the grammar, without consecutive hyphens.</summary>
    private static bool IsValidHolder(string holder)
        => holder.Length == 0 || (HolderRegex().IsMatch(holder) && !holder.Contains("--", StringComparison.Ordinal));

    /// <summary>
    /// Rejects rooted paths, backslashes and any <c>..</c> segment, so a key can never escape
    /// the container an adapter resolves it into (defence in depth, independent of the backend).
    /// </summary>
    private static bool IsSafeKey(string key)
    {
        if (key[0] is '/' or '\\') return false;
        if (key.Contains('\\', StringComparison.Ordinal)) return false;

        foreach (var segment in key.Split('/'))
        {
            if (segment.Length == 0 || segment == "." || segment == "..")
                return false;
        }

        return true;
    }

    [GeneratedRegex("^[a-z][a-z0-9-]{2,62}$")]
    private static partial Regex HolderRegex();

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex NameRegex();
}
