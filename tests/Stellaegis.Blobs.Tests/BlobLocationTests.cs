using Stellaegis.Primitives;
using Xunit;

namespace Stellaegis.Blobs.Tests;

/// <summary>
/// Unit tests for <see cref="BlobLocation"/>: its space, a compartment or the shared space, and the key grammar. The compartment grammar belongs to <see cref="Compartment"/> and is tested with it.
/// </summary>
public sealed class BlobLocationTests
{
    private static readonly Compartment Acme = Compartment.Parse("acme");

    [Fact]
    public void Compartment_converts_to_its_space()
    {
        var location = new BlobLocation(Acme, "a.pdf");

        Assert.Equal(Space.Of(Acme), location.Space);
        Assert.Equal(Acme, location.Space.Compartment);
        Assert.False(location.Space.IsShared);
    }

    [Fact]
    public void Missing_space_is_refused_rather_than_read_as_shared()
    {
        var exception = Assert.Throws<ArgumentNullException>(() => new BlobLocation(null!, "a.pdf"));

        Assert.Equal("space", exception.ParamName);
    }

    [Fact]
    public void Missing_compartment_is_refused_rather_than_read_as_shared()
    {
        // A compartment that could not be resolved converts to a null space, never to the shared one.
        Compartment unresolved = null!;

        Assert.Throws<ArgumentNullException>(() => new BlobLocation(unresolved, "a.pdf"));
    }

    [Fact]
    public void Shared_location_has_no_compartment()
    {
        var location = new BlobLocation(Space.Shared, "a.pdf");

        Assert.True(location.Space.IsShared);
        Assert.Null(location.Space.Compartment);
        Assert.Equal(location, new BlobLocation(Space.Shared, "a.pdf"));
    }

    [Fact]
    public void Shared_space_differs_from_a_compartment_named_shared()
        => Assert.NotEqual(
            new BlobLocation(Space.Shared, "a.pdf"),
            new BlobLocation(Compartment.Parse("shared"), "a.pdf"));

    [Fact]
    public void Shared_location_validates_its_key()
        => Assert.Equal("key", Assert.Throws<ArgumentException>(() => new BlobLocation(Space.Shared, "../a.pdf")).ParamName);

    [Theory]
    [InlineData("a.pdf")]
    [InlineData("2026/06/08/3f2a.pdf")]
    [InlineData("3f2a9c/v1")]
    [InlineData("..a/b")]
    [InlineData("my file_v2 (final)+#@=,;!~.pdf")]
    [InlineData("console/nullable/com10/lpt.txt/con-1")]
    public void Valid_key_is_accepted(string key)
        => Assert.Equal(key, new BlobLocation(Acme, key).Key);

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
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation(Acme, key));

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
        var exception = Assert.Throws<ArgumentException>(() => new BlobLocation(Acme, key));

        Assert.Equal("key", exception.ParamName);
    }

    [Theory]
    [InlineData("a./b.pdf")]
    [InlineData("a.pdf.")]
    [InlineData("a /b.pdf")]
    [InlineData("a.pdf ")]
    public void Segment_ending_with_a_dot_or_a_space_is_refused(string key)
        => Assert.Throws<ArgumentException>(() => new BlobLocation(Acme, key));

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
        => Assert.Throws<ArgumentException>(() => new BlobLocation(Acme, key));

    [Fact]
    public void Key_length_is_bounded_overall_and_per_segment()
    {
        var segment = new string('a', BlobLocation.MaxSegmentLength);
        Assert.Equal(segment, new BlobLocation(Acme, segment).Key);
        Assert.Throws<ArgumentException>(() => new BlobLocation(Acme, segment + "a"));

        // Three 255-character segments, one of 254 and a last of 1, with four slashes: exactly 1024 characters.
        var atLimit = string.Join('/', segment, segment, segment, segment[..254]) + "/a";
        Assert.Equal(BlobLocation.MaxKeyLength, atLimit.Length);
        Assert.Equal(atLimit, new BlobLocation(Acme, atLimit).Key);
        Assert.Throws<ArgumentException>(() => new BlobLocation(Acme, atLimit + "a"));
    }
}
