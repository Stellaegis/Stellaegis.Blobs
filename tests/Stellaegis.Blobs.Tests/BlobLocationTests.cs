using Xunit;

namespace Stellaegis.Blobs.Tests;

/// <summary>
/// Unit tests for <see cref="BlobLocation"/>: the holder, space name and key grammars, and the platform value.
/// </summary>
public sealed class BlobLocationTests
{
    [Theory]
    [InlineData("acme")]
    [InlineData("a1-b2")]
    [InlineData("abc")]
    [InlineData("a23456789012345678901234567890123456789012345678901234567890123")]
    public void Valid_holder_is_accepted(string holder)
    {
        var location = new BlobLocation(holder, "files", "a.pdf");

        Assert.Equal(holder, location.Holder);
        Assert.False(location.IsPlatform);
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("a234567890123456789012345678901234567890123456789012345678901234")]
    [InlineData("Acme")]
    [InlineData("1acme")]
    [InlineData("-acme")]
    [InlineData("ac--me")]
    [InlineData("ac_me")]
    [InlineData("ac/me")]
    [InlineData(" ")]
    public void Invalid_holder_is_refused(string holder)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation(holder, "files", "a.pdf"));

        Assert.Equal("holder", exception.ParamName);
    }

    [Fact]
    public void Null_holder_is_refused()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new BlobLocation(null!, "files", "a.pdf"));

        Assert.Equal("holder", exception.ParamName);
    }

    [Fact]
    public void Platform_is_the_empty_holder()
    {
        var location = BlobLocation.ForPlatform("files", "a.pdf");

        Assert.Equal(BlobLocation.Platform, location.Holder);
        Assert.Equal("", location.Holder);
        Assert.True(location.IsPlatform);
        Assert.Equal(location, new BlobLocation("", "files", "a.pdf"));
    }

    [Theory]
    [InlineData("f")]
    [InlineData("files")]
    [InlineData("ged-2")]
    public void Valid_space_name_is_accepted(string name)
        => Assert.Equal(name, new BlobLocation("acme", name, "a.pdf").Name);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Files")]
    [InlineData("2files")]
    [InlineData("my_files")]
    [InlineData("my/files")]
    public void Invalid_space_name_is_refused(string name)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation("acme", name, "a.pdf"));

        Assert.Equal("name", exception.ParamName);
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("2026/06/08/a.pdf")]
    [InlineData("..a/b..")]
    public void Valid_key_is_accepted(string key)
        => Assert.Equal(key, new BlobLocation("acme", "files", key).Key);

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("/a.pdf")]
    [InlineData("\\a.pdf")]
    [InlineData("a\\b.pdf")]
    [InlineData("a//b.pdf")]
    [InlineData("a/")]
    [InlineData("./a.pdf")]
    [InlineData("a/../b.pdf")]
    [InlineData("..")]
    public void Unsafe_key_is_refused(string key)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", key));

        Assert.Equal("key", exception.ParamName);
    }
}
