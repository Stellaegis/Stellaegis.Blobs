using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging;
using Stellaegis.Blobs.FileSystem;
using Xunit;

namespace Stellaegis.Blobs.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddStellaegisFileSystemBlobStore"/>.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    [Fact]
    public void Registers_the_filesystem_store_once_as_a_singleton()
    {
        var root = Path.Combine(Path.GetTempPath(), "stellaegis-blobs-tests", Guid.NewGuid().ToString("N"));
        var services = new ServiceCollection();
        services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

        services.AddStellaegisFileSystemBlobStore(o => o.RootPath = root);
        services.AddStellaegisFileSystemBlobStore(o => o.RootPath = root);

        Assert.Single(services, d => d.ServiceType == typeof(IBlobStore));

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IBlobStore>();
        Assert.IsType<FileSystemBlobStore>(store);
        Assert.Same(store, provider.GetRequiredService<IBlobStore>());
    }
}
