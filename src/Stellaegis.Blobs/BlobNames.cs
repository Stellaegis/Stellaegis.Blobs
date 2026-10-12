namespace Stellaegis.Blobs;

/// <summary>
/// Canonical blob space names (the <c>name</c> discriminant of a <see cref="BlobLocation"/>).
/// </summary>
/// <remarks>
/// These mirror the <c>name</c> segment of a tenant database key
/// (<c>{tier}.{name}.{provider}.{tenantSlug}</c>): framework-owned, closed set —
/// unlike tenant database names, which the developer declares freely.
/// </remarks>
public static class BlobNames
{
    /// <summary>Document templates (system defaults and tenant overrides). Schema <c>doc.templates</c>.</summary>
    public const string Templates = "templates";

    /// <summary>Managed documents (GED). Schema <c>doc.documents</c>.</summary>
    public const string Documents = "ged";

    /// <summary>Trashed item snapshots. Schema <c>trash.items</c>.</summary>
    public const string Trash = "trash";
}
