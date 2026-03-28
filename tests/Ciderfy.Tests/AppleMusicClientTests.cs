using System.Text.Json;
using Ciderfy.Apple;
using Xunit;

namespace Ciderfy.Tests;

public class AppleMusicClientTests
{
    private static JsonElement ParseElement(string json) => JsonDocument.Parse(json).RootElement;

    [Fact]
    public void ParseTrack_ValidElement_ReturnsTrack()
    {
        var element = ParseElement(
            """
            {
              "id": "123456",
              "attributes": {
                "name": "Fortunate Son",
                "artistName": "Creedence Clearwater Revival",
                "durationInMillis": 140000,
                "isrc": "USABC1234567"
              }
            }
            """
        );

        var track = AppleMusicClient.ParseTrack(element);

        Assert.NotNull(track);
        Assert.Equal("123456", track.Id);
        Assert.Equal("Fortunate Son", track.Title);
        Assert.Equal("Creedence Clearwater Revival", track.Artist);
        Assert.Equal(140000, track.DurationMs);
        Assert.Equal("USABC1234567", track.Isrc);
    }

    [Fact]
    public void ParseTrack_MissingId_ReturnsNull()
    {
        var element = ParseElement(
            """
            {
              "attributes": {
                "name": "Fortunate Son",
                "artistName": "CCR"
              }
            }
            """
        );

        Assert.Null(AppleMusicClient.ParseTrack(element));
    }

    [Fact]
    public void ParseTrack_MissingAttributes_ReturnsNull()
    {
        var element = ParseElement("""{ "id": "123456" }""");

        Assert.Null(AppleMusicClient.ParseTrack(element));
    }

    [Fact]
    public void ParseTrack_MissingOptionalFields_UsesDefaults()
    {
        var element = ParseElement(
            """
            {
              "id": "123456",
              "attributes": {}
            }
            """
        );

        var track = AppleMusicClient.ParseTrack(element);

        Assert.NotNull(track);
        Assert.Equal("123456", track.Id);
        Assert.Equal(string.Empty, track.Title);
        Assert.Equal(string.Empty, track.Artist);
        Assert.Equal(0, track.DurationMs);
        Assert.Null(track.Isrc);
    }

    [Fact]
    public void ParseTrack_NoIsrc_ReturnsTrackWithNullIsrc()
    {
        var element = ParseElement(
            """
            {
              "id": "abc",
              "attributes": {
                "name": "Song",
                "artistName": "Artist",
                "durationInMillis": 200000
              }
            }
            """
        );

        var track = AppleMusicClient.ParseTrack(element);

        Assert.NotNull(track);
        Assert.Null(track.Isrc);
    }
}
