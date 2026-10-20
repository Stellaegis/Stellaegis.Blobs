using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Options for the filesystem blob adapter, bound from <c>Stellaegis:Blobs:FileSystem</c> and validated when the
/// host starts: a missing root fails the host, not the first write.
/// </summary>
public sealed class FileSystemBlobOptions : IValidatableOptions
{
    /// <summary>Configuration section binding path.</summary>
    public const string SectionName = "Stellaegis:Blobs:FileSystem";

    /// <summary>
    /// Absolute root directory under which one directory per space is created
    /// (<c>{root}/{compartment}</c>, or <c>{root}/_shared</c> for the shared space).
    /// </summary>
    public string RootPath { get; set; } = "";

    /// <inheritdoc />
    public string? FindError() => string.IsNullOrWhiteSpace(RootPath) ? $"{nameof(RootPath)} is required." : null;
}
