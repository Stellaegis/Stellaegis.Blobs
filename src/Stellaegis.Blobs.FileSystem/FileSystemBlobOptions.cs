namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Options for the filesystem blob adapter.
/// </summary>
public sealed class FileSystemBlobOptions
{
    /// <summary>Configuration section binding path.</summary>
    public const string SectionName = "Stellaegis:Blobs:FileSystem";

    /// <summary>
    /// Absolute root directory under which one directory per space is created
    /// (<c>{root}/{compartment}</c>, or <c>{root}/_shared</c> for the shared space).
    /// </summary>
    public string RootPath { get; set; } = "";
}
