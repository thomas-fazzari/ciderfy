using System.Text.Json;
using Ciderfy.Spotify;
using Xunit;

namespace Ciderfy.Tests;

public class SpotifyClientTests
{
    [Fact]
    public void ParsePlaylistPage_ValidResponse_ReturnsNameAndTracks()
    {
        const string json = """
            {
              "data": {
                "playlistV2": {
                  "name": "My Playlist",
                  "content": {
                    "items": [
                      {
                        "itemV2": {
                          "data": {
                            "name": "Fortunate Son",
                            "uri": "spotify:track:abc123",
                            "artists": { "items": [{ "profile": { "name": "Creedence Clearwater Revival" } }] },
                            "trackDuration": { "totalMilliseconds": 140000 }
                          }
                        }
                      }
                    ]
                  }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Equal("My Playlist", result.Name);
        Assert.Single(result.Tracks);
        Assert.Equal("Fortunate Son", result.Tracks[0].Title);
        Assert.Equal("Creedence Clearwater Revival", result.Tracks[0].Artist);
        Assert.Equal("abc123", result.Tracks[0].SpotifyId);
        Assert.Equal(140000, result.Tracks[0].DurationMs);
    }

    [Fact]
    public void ParsePlaylistPage_MissingDataKey_ReturnsDefaultNameEmptyTracks()
    {
        using var doc = JsonDocument.Parse("{}");
        var result = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Equal("Spotify Import", result.Name);
        Assert.Empty(result.Tracks);
    }

    [Fact]
    public void ParsePlaylistPage_MissingPlaylistName_UsesDefaultName()
    {
        const string json = """
            {
              "data": {
                "playlistV2": {
                  "content": { "items": [] }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Equal("Spotify Import", result.Name);
        Assert.Empty(result.Tracks);
    }

    [Fact]
    public void ParsePlaylistPage_MissingContentKey_ReturnsEmptyTracks()
    {
        const string json = """{ "data": { "playlistV2": { "name": "My Playlist" } } }""";

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Equal("My Playlist", result.Name);
        Assert.Empty(result.Tracks);
    }

    [Fact]
    public void ParsePlaylistPage_SkipsItemsWithMissingTitle()
    {
        const string json = """
            {
              "data": {
                "playlistV2": {
                  "name": "Playlist",
                  "content": {
                    "items": [
                      { "itemV2": { "data": { "uri": "spotify:track:x" } } }
                    ]
                  }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Empty(result.Tracks);
    }

    // ParsePlaylistItem

    [Fact]
    public void ParsePlaylistItem_ValidItem_ReturnsTrack()
    {
        const string json = """
            {
              "itemV2": {
                "data": {
                  "name": "War Pigs",
                  "uri": "spotify:track:wp456",
                  "artists": { "items": [{ "profile": { "name": "Black Sabbath" } }] },
                  "trackDuration": { "totalMilliseconds": 476000 }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ParsePlaylistItem(doc.RootElement);

        Assert.NotNull(result);
        Assert.Equal("War Pigs", result.Title);
        Assert.Equal("Black Sabbath", result.Artist);
        Assert.Equal("wp456", result.SpotifyId);
        Assert.Equal(476000, result.DurationMs);
    }

    [Fact]
    public void ParsePlaylistItem_MissingItemV2_ReturnsNull()
    {
        using var doc = JsonDocument.Parse("{}");
        var result = SpotifyClient.ParsePlaylistItem(doc.RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void ParsePlaylistItem_EmptyTitle_ReturnsNull()
    {
        const string json =
            """{ "itemV2": { "data": { "name": "", "uri": "spotify:track:x" } } }""";

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ParsePlaylistItem(doc.RootElement);

        Assert.Null(result);
    }

    [Fact]
    public void ExtractFirstArtist_ArtistsKey_ReturnsName()
    {
        const string json = """
            { "artists": { "items": [{ "profile": { "name": "Daft Punk" } }] } }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ExtractFirstArtist(doc.RootElement);

        Assert.Equal("Daft Punk", result);
    }

    [Fact]
    public void ExtractFirstArtist_FallsBackToFirstArtistKey()
    {
        const string json = """
            { "firstArtist": { "items": [{ "profile": { "name": "Queen" } }] } }
            """;

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ExtractFirstArtist(doc.RootElement);

        Assert.Equal("Queen", result);
    }

    [Fact]
    public void ExtractFirstArtist_NeitherKey_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("{}");
        var result = SpotifyClient.ExtractFirstArtist(doc.RootElement);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractIdFromUri_ValidUri_ReturnsLastSegment()
    {
        const string json = """{ "uri": "spotify:track:abc123" }""";

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ExtractIdFromUri(doc.RootElement);

        Assert.Equal("abc123", result);
    }

    [Fact]
    public void ExtractIdFromUri_MissingUri_ReturnsEmpty()
    {
        using var doc = JsonDocument.Parse("{}");
        var result = SpotifyClient.ExtractIdFromUri(doc.RootElement);

        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ExtractDuration_NumericMilliseconds_ReturnsValue()
    {
        const string json = """{ "trackDuration": { "totalMilliseconds": 210000 } }""";

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ExtractDuration(doc.RootElement);

        Assert.Equal(210000, result);
    }

    [Fact]
    public void ExtractDuration_StringMilliseconds_ReturnsValue()
    {
        const string json = """{ "trackDuration": { "totalMilliseconds": "210000" } }""";

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ExtractDuration(doc.RootElement);

        Assert.Equal(210000, result);
    }

    [Fact]
    public void ExtractDuration_MissingTrackDuration_ReturnsZero()
    {
        using var doc = JsonDocument.Parse("{}");
        var result = SpotifyClient.ExtractDuration(doc.RootElement);

        Assert.Equal(0, result);
    }

    [Fact]
    public void ExtractDuration_UnexpectedValueKind_ReturnsZero()
    {
        const string json = """{ "trackDuration": { "totalMilliseconds": null } }""";

        using var doc = JsonDocument.Parse(json);
        var result = SpotifyClient.ExtractDuration(doc.RootElement);

        Assert.Equal(0, result);
    }

    // GenerateTotpCode

    [Fact]
    public void GenerateTotpCode_ReturnsNonEmptySixDigitCode()
    {
        var code = SpotifyClient.GenerateTotpCode();

        Assert.NotNull(code);
        Assert.Equal(6, code.Length);
        Assert.True(int.TryParse(code, out _));
    }

    [Fact]
    public void GenerateTotpCode_TwoCallsWithinSameStep_ReturnSameCode()
    {
        var code1 = SpotifyClient.GenerateTotpCode();
        var code2 = SpotifyClient.GenerateTotpCode();

        // Both calls happen within the same 30s TOTP window
        Assert.Equal(code1, code2);
    }

    [Fact]
    public void ParsePlaylistPage_WithTotalCount_ReturnsTotalCount()
    {
        const string json = """
            {
              "data": {
                "playlistV2": {
                  "name": "Big Playlist",
                  "content": {
                    "totalCount": 1308,
                    "items": [],
                    "pagingInfo": { "offset": 0, "limit": 300 }
                  }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var (name, tracks, totalCount) = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Equal("Big Playlist", name);
        Assert.Empty(tracks);
        Assert.Equal(1308, totalCount);
    }

    [Fact]
    public void ParsePlaylistPage_MissingTotalCount_FallsBackToItemCount()
    {
        const string json = """
            {
              "data": {
                "playlistV2": {
                  "name": "Small Playlist",
                  "content": {
                    "items": [
                      {
                        "itemV2": {
                          "data": {
                            "name": "Song",
                            "uri": "spotify:track:x1",
                            "artists": { "items": [{ "profile": { "name": "Artist" } }] },
                            "trackDuration": { "totalMilliseconds": 200000 }
                          }
                        }
                      }
                    ]
                  }
                }
              }
            }
            """;

        using var doc = JsonDocument.Parse(json);
        var (_, tracks, totalCount) = SpotifyClient.ParsePlaylistPage(doc);

        Assert.Single(tracks);
        Assert.Equal(1, totalCount);
    }
}
