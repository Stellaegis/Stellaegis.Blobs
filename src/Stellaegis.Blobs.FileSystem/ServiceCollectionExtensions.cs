using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Dependency injection helpers for the filesystem blob adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileSystemBlobStore"/> as the singleton <see cref="IBlobStore"/>.
    /// Intended for development; production uses a cloud object-storage adapter.
    /// </summary>
    public static IServiceCollection AddStellaegisFileSystemBlobStore(
        this IServiceCollection services,
        Action<FileSystemBlobOptions> configure)
    {
        Guard.NotNull(configure);

        services.Configure(configure);
        services.TryAddSingleton<IBlobStore, FileSystemBlobStore>();
        return services;
    }
}
