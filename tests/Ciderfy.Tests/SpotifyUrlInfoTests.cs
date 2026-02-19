using Ciderfy.Spotify;
using Xunit;

namespace Ciderfy.Tests;

public class SpotifyUrlInfoTests
{
    [Theory]
    [InlineData(
        "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M",
        "Playlist",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    [InlineData(
        "https://open.spotify.com/track/6rqhFgbbKwnb9MLmUQDhG6",
        "Track",
        "6rqhFgbbKwnb9MLmUQDhG6"
    )]
    [InlineData(
        "https://open.spotify.com/album/4aawyAB9vmqN3uQ7FjRGTy",
        "Album",
        "4aawyAB9vmqN3uQ7FjRGTy"
    )]
    [InlineData(
        "https://open.spotify.com/embed/playlist/37i9dQZF1DXcBWIGoYBM5M",
        "Playlist",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    [InlineData(
        "https://open.spotify.com/embed/track/6rqhFgbbKwnb9MLmUQDhG6",
        "Track",
        "6rqhFgbbKwnb9MLmUQDhG6"
    )]
    [InlineData(
        "https://open.spotify.com/intl-fr/playlist/37i9dQZF1DXcBWIGoYBM5M",
        "Playlist",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    [InlineData(
        "https://open.spotify.com/intl-de/track/6rqhFgbbKwnb9MLmUQDhG6",
        "Track",
        "6rqhFgbbKwnb9MLmUQDhG6"
    )]
    [InlineData("spotify:playlist:37i9dQZF1DXcBWIGoYBM5M", "Playlist", "37i9dQZF1DXcBWIGoYBM5M")]
    [InlineData("spotify:track:6rqhFgbbKwnb9MLmUQDhG6", "Track", "6rqhFgbbKwnb9MLmUQDhG6")]
    [InlineData("spotify:album:4aawyAB9vmqN3uQ7FjRGTy", "Album", "4aawyAB9vmqN3uQ7FjRGTy")]
    [InlineData(
        "https://open.spotify.com/playlist/37i9dQZF1DXcBWIGoYBM5M?si=abc123",
        "Playlist",
        "37i9dQZF1DXcBWIGoYBM5M"
    )]
    public void TryParse_ValidInput_ReturnsCorrectTypeAndId(
        string url,
        string expectedType,
        string expectedId
    )
    {
        var success = SpotifyUrlInfo.TryParse(url, out var result);

        Assert.True(success);
        Assert.NotNull(result);
        Assert.Equal(Enum.Parse<SpotifyUrlType>(expectedType), result.Type);
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
        Assert.Equal(SpotifyUrlType.Playlist, result.Type);
        Assert.Equal("abc123", result.Id);
    }

    [Fact]
    public void Parse_InvalidUrl_ReturnsNull()
    {
        var result = SpotifyUrlInfo.Parse("not-valid");

        Assert.Null(result);
    }
}
