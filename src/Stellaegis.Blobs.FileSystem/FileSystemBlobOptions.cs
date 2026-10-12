namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Options for the filesystem blob adapter.
/// </summary>
public sealed class FileSystemBlobOptions
{
    /// <summary>Configuration section binding path.</summary>
    public const string SectionName = "Stellaegis:Blobs:FileSystem";

    /// <summary>
    /// Absolute root directory under which per-space containers are created
    /// (<c>{root}/{holder}_{name}</c>, or <c>{root}/_platform_{name}</c> for the platform's own space).
    /// </summary>
    public string RootPath { get; set; } = "";
}
