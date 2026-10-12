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

    [Fact]
    public void Space_name_is_at_most_63_characters()
    {
        Assert.Equal(63, new BlobLocation("acme", "f" + new string('a', 62), "a.pdf").Name.Length);
        Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "f" + new string('a', 63), "a.pdf"));
    }

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("2026/06/08/3f2a.pdf")]
    [InlineData("3f2a9c/v1")]
    [InlineData("..a/b")]
    [InlineData("my file_v2 (final)+#@=,;!~.pdf")]
    [InlineData("console/nullable/com10/lpt.txt/con-1")]
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
    public void Key_escaping_its_space_is_refused(string key)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", key));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData("Invoice.pdf")]
    [InlineData("facture-été.pdf")]
    [InlineData("a:b.pdf")]
    [InlineData("a<b.pdf")]
    [InlineData("a>b.pdf")]
    [InlineData("a\"b.pdf")]
    [InlineData("a|b.pdf")]
    [InlineData("a?b.pdf")]
    [InlineData("a*b.pdf")]
    [InlineData("a\tb.pdf")]
    [InlineData("a\u007fb.pdf")]
    public void Key_with_a_character_some_backend_refuses_is_refused(string key)
    {
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", key));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData("a./b.pdf")]
    [InlineData("a.pdf.")]
    [InlineData("a /b.pdf")]
    [InlineData("a.pdf ")]
    public void Segment_ending_with_a_dot_or_a_space_is_refused(string key)
        => Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", key));

    [Theory]
    [InlineData("con")]
    [InlineData("prn")]
    [InlineData("aux")]
    [InlineData("nul")]
    [InlineData("com1")]
    [InlineData("lpt9")]
    [InlineData("nul.txt")]
    [InlineData("con.tar.gz")]
    [InlineData("dir/aux.pdf")]
    [InlineData("nul .txt")]
    public void Windows_device_name_is_refused(string key)
        => Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", key));

    [Fact]
    public void Key_length_is_bounded_overall_and_per_segment()
    {
        var segment = new string('a', BlobLocation.MaxSegmentLength);
        Assert.Equal(segment, new BlobLocation("acme", "files", segment).Key);
        Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", segment + "a"));

        // Three 255-character segments, one of 254 and a last of 1, with four slashes: exactly 1024 characters.
        var atLimit = string.Join('/', segment, segment, segment, segment[..254]) + "/a";
        Assert.Equal(BlobLocation.MaxKeyLength, atLimit.Length);
        Assert.Equal(atLimit, new BlobLocation("acme", "files", atLimit).Key);
        Assert.Throws<ArgumentException>(() => new BlobLocation("acme", "files", atLimit + "a"));
    }
}
