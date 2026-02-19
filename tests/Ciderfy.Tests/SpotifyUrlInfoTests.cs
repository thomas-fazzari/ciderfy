using Ciderfy.Spotify;
using Xunit;

namespace Ciderfy.Tests;

public class SpotifyUrlInfoTests
{
    [Theory]
    [InlineData(
        "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    [InlineData(
        "https://open.spotify.com/embed/playlist/37i9dQZF1DXcBWIGoYBM5M",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    [InlineData(
        "https://open.spotify.com/intl-fr/playlist/37i9dQZF1DXcBWIGoYBM5M",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData(
        "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=abc123",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    public void TryParse_ValidPlaylist_ReturnsId(string url, string expectedId)
    {
        var success = SpotifyUrlInfo.TryParse(url, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(expectedId, result.Id);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("https://example.com")]
    [InlineData("not-a-url")]
    [InlineData("spotify:")]
    [InlineData("spotify:invalid")]
    [InlineData("https://open.spotify.com/")]
    [InlineData("https://open.spotify.com/unknown/123")]
    [InlineData("https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData("spotify:track:6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("spotify:album:4aawyAB9vmqN3uQ7FjRGTy")]
    public void TryParse_InvalidInput_ReturnsFalse(string? url)
    {
        var success = SpotifyUrlInfo.TryParse(url, out var result);

        Assert.False(success);
        Assert.Null(result);
    }

    [Fact]
    public void Parse_ValidUrl_ReturnsInfo()
    {
        var result = SpotifyUrlInfo.Parse("spotify:playlist:abc123");

        Assert.NotNull(result);
        Assert.Equal("abc123", result.Id);
    }

    [Fact]
    public void Parse_InvalidUrl_ReturnsNull()
    {
        var result = SpotifyUrlInfo.Parse("not-valid");

        Assert.Null(result);
    }
}
