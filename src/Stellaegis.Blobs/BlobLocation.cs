using System.Text.RegularExpressions;

namespace Stellaegis.Blobs;

/// <summary>
/// Addresses a blob by the two stable, non-physical layers Stellaegis uses for tenant storage:
/// <list type="bullet">
///   <item><b>Who</b> — <see cref="TenantSlug"/> (the empty-string sentinel = the framework system space)
///   and <see cref="Name"/> (the blob space, e.g. <c>templates</c>/<c>ged</c>/<c>trash</c>).
///   Together they select the <b>binding</b> (which backend/account/credentials hosts the blob).</item>
///   <item><b>What</b> — <see cref="Key"/>, the logical object path <i>within</i> that space.</item>
/// </list>
/// The physical provider, endpoint, container and credentials are never part of a location:
/// they are resolved per tenant by the <see cref="IBlobStore"/> implementation (default installation
/// config, or a per-tenant vault binding for "bring your own blob storage"). This keeps stored
/// keys stable across a backend migration and free of any secret.
/// </summary>
/// <remarks>
/// A location with <see cref="IsSystem"/> = <see langword="true"/> designates shared, framework-owned
/// content (e.g. a default invoice template) — the same <c>tenant_slug = ''</c> sentinel used by the
/// <c>doc.templates</c> resolution.
/// </remarks>
public sealed partial record BlobLocation
{
    /// <summary>The sentinel <see cref="TenantSlug"/> value designating the framework system space.</summary>
    public const string SystemTenant = "";

    /// <summary>
    /// Tenant slug owning the blob, or <see cref="SystemTenant"/> (empty string) for shared
    /// framework-owned content. Never collides with a real slug (which must start with a letter).
    /// </summary>
    public string TenantSlug { get; }

    /// <summary>Blob space discriminant — see <see cref="BlobNames"/>.</summary>
    public string Name { get; }

    /// <summary>
    /// Logical object key within the tenant's blob space (e.g. <c>standard_invoice.scriban</c>
    /// or <c>2026/06/08/{id}.pdf</c>). Forward-slash separated, relative, never rooted.
    /// </summary>
    public string Key { get; }

    /// <summary>Creates a validated blob location.</summary>
    /// <exception cref="ArgumentException">A segment is empty, malformed, or the key escapes its space.</exception>
    public BlobLocation(string tenantSlug, string name, string key)
    {
        ArgumentNullException.ThrowIfNull(tenantSlug);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (tenantSlug.Length != 0 && (!TenantSlugRegex().IsMatch(tenantSlug) || tenantSlug.Contains("--", StringComparison.Ordinal)))
            throw new ArgumentException(
                $"'{tenantSlug}' is not a valid tenant slug (or the system sentinel '').", nameof(tenantSlug));

        if (!NameRegex().IsMatch(name))
            throw new ArgumentException($"'{name}' is not a valid blob space name.", nameof(name));

        if (!IsSafeKey(key))
            throw new ArgumentException(
                $"'{key}' is not a valid blob key (must be relative, forward-slash separated, no '..').", nameof(key));

        TenantSlug = tenantSlug;
        Name = name;
        Key = key;
    }

    /// <summary>Creates a location in the shared framework system space (<see cref="SystemTenant"/>).</summary>
    public static BlobLocation System(string name, string key) => new(SystemTenant, name, key);

    /// <summary><see langword="true"/> when this location designates the framework system space.</summary>
    public bool IsSystem => TenantSlug.Length == 0;

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
    private static partial Regex TenantSlugRegex();

    [GeneratedRegex("^[a-z][a-z0-9-]*$")]
    private static partial Regex NameRegex();
}
