using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Stellaegis.Blobs.FileSystem;
using Xunit;

namespace Stellaegis.Blobs.Tests;

/// <summary>
/// Unit tests for <see cref="ServiceCollectionExtensions.AddStellaegisFileSystemBlobStore(IServiceCollection, Action{FileSystemBlobOptions})"/>
/// and its configuration overload: one singleton store, options bound from their section, validated at startup.
/// </summary>
public sealed class ServiceCollectionExtensionsTests
{
    private static string NewRoot() => Path.Combine(Path.GetTempPath(), "stellaegis-blobs-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public void Registers_the_filesystem_store_once_as_a_singleton()
    {
        var root = NewRoot();
        var services = new ServiceCollection();

        services.AddStellaegisFileSystemBlobStore(o => o.RootPath = root);
        services.AddStellaegisFileSystemBlobStore(o => o.RootPath = root);

        Assert.Single(services, d => d.ServiceType == typeof(IBlobStore));

        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IBlobStore>();
        Assert.IsType<FileSystemBlobStore>(store);
        Assert.Same(store, provider.GetRequiredService<IBlobStore>());
    }

    [Fact]
    public void Binds_the_options_from_their_configuration_section()
    {
        var root = NewRoot();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection([new KeyValuePair<string, string?>("Stellaegis:Blobs:FileSystem:RootPath", root)])
            .Build();

        using var provider = new ServiceCollection()
            .AddStellaegisFileSystemBlobStore(configuration)
            .BuildServiceProvider();

        Assert.Equal(root, provider.GetRequiredService<IOptions<FileSystemBlobOptions>>().Value.RootPath);
        Assert.IsType<FileSystemBlobStore>(provider.GetRequiredService<IBlobStore>());
    }

    // The root is validated when the options are built, which a host does when it starts: not at the first write.
    [Fact]
    public void A_missing_root_fails_the_options_before_the_store_is_built()
    {
        using var configured = new ServiceCollection().AddStellaegisFileSystemBlobStore(o => { }).BuildServiceProvider();
        var exception = Assert.Throws<OptionsValidationException>(() => configured.GetRequiredService<IBlobStore>());
        Assert.Contains("RootPath is required", exception.Message);

        using var bound = new ServiceCollection()
            .AddStellaegisFileSystemBlobStore(new ConfigurationBuilder().Build())
            .BuildServiceProvider();
        Assert.Throws<OptionsValidationException>(() => bound.GetRequiredService<IBlobStore>());
    }
}
