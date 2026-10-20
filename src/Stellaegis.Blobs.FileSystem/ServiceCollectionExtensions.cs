using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Stellaegis.Primitives.Hosting;

namespace Stellaegis.Blobs.FileSystem;

/// <summary>
/// Dependency injection helpers for the filesystem blob adapter.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="FileSystemBlobStore"/> as the singleton <see cref="IBlobStore"/>, its options configured in
    /// code and validated when the host starts. Suited to development, tests and applications that run on a single
    /// machine; a second call keeps the first registration.
    /// </summary>
    public static IServiceCollection AddStellaegisFileSystemBlobStore(
        this IServiceCollection services,
        Action<FileSystemBlobOptions> configure)
    {
        Guard.NotNull(services);
        Guard.NotNull(configure);

        services.AddStellaegisOptions(configure);
        services.TryAddSingleton<IBlobStore, FileSystemBlobStore>();
        return services;
    }

    /// <summary>
    /// Registers <see cref="FileSystemBlobStore"/> as the singleton <see cref="IBlobStore"/>, its options bound from
    /// the section <paramref name="sectionName"/> of <paramref name="configuration"/> —
    /// <see cref="FileSystemBlobOptions.SectionName"/> unless stated — and validated when the host starts.
    /// </summary>
    public static IServiceCollection AddStellaegisFileSystemBlobStore(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName = FileSystemBlobOptions.SectionName)
    {
        Guard.NotNull(services);
        Guard.NotNull(configuration);

        services.AddStellaegisOptions<FileSystemBlobOptions>(configuration, sectionName);
        services.TryAddSingleton<IBlobStore, FileSystemBlobStore>();
        return services;
    }
}
