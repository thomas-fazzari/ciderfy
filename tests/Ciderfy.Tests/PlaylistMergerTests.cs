using Ciderfy.Matching;
using Ciderfy.Spotify;
using Ciderfy.Tests.Fakers;
using Xunit;

namespace Ciderfy.Tests;

public class PlaylistMergerTests
{
    [Fact]
    public void MergeTracks_TwoPlaylistsNoOverlap_ReturnsConcatenatedTracks()
    {
        var tracksA = SpotifyTrackFaker.Default.Generate(2);
        var tracksB = SpotifyTrackFaker.Default.Generate(2);
        var p1 = new SpotifyPlaylist("P1", tracksA);
        var p2 = new SpotifyPlaylist("P2", tracksB);

        var result = PlaylistMerger.MergeTracks([p1, p2]);

        Assert.Equal(4, result.Count);
        // P1 first, then P2
        Assert.Equal(tracksA[0].SpotifyId, result[0].SpotifyId);
        Assert.Equal(tracksA[1].SpotifyId, result[1].SpotifyId);
        Assert.Equal(tracksB[0].SpotifyId, result[2].SpotifyId);
        Assert.Equal(tracksB[1].SpotifyId, result[3].SpotifyId);
    }

    [Fact]
    public void MergeTracks_DuplicateAppearsInFirstPlaylist_KeepsFirstOccurrence()
    {
        var canonical = SpotifyTrackFaker.Default.Generate();
        var duplicate = canonical with { Title = "Duplicate Title", Artist = "Other Artist" };

        var p1 = new SpotifyPlaylist("P1", [canonical]);
        var p2 = new SpotifyPlaylist("P2", [duplicate]);

        var result = PlaylistMerger.MergeTracks([p1, p2]);

        var kept = Assert.Single(result);
        Assert.Equal(canonical.Title, kept.Title);
    }

    [Fact]
    public void MergeTracks_EmptyPlaylist_ReturnsEmpty()
    {
        var result = PlaylistMerger.MergeTracks([PlaylistFaker.WithTracks(0).Generate()]);

        Assert.Empty(result);
    }

    [Fact]
    public void MergeTracks_PreservesIsrc()
    {
        var track = SpotifyTrackFaker.WithIsrc.Generate();

        var result = PlaylistMerger.MergeTracks([new SpotifyPlaylist("P", [track])]);

        Assert.Equal(track.Isrc, result[0].Isrc);
        Assert.NotNull(result[0].Isrc);
    }

    [Fact]
    public void ResolveName_UserOverride_ReturnsOverride()
    {
        var playlists = PlaylistFaker.Default.Generate(2);

        var name = PlaylistMerger.ResolveName(playlists, "My Custom Name");

        Assert.Equal("My Custom Name", name);
    }

    [Fact]
    public void ResolveName_SinglePlaylistNoOverride_ReturnsPlaylistName()
    {
        var playlist = PlaylistFaker.WithName("Summer Vibes").Generate();

        var name = PlaylistMerger.ResolveName([playlist], null);

        Assert.Equal("Summer Vibes", name);
    }

    [Fact]
    public void ResolveName_MultiplePlaylistsNoOverride_ReturnsMergedWithDate()
    {
        var playlists = PlaylistFaker.Default.Generate(2);
        var today = new DateOnly(2026, 2, 25);

        var name = PlaylistMerger.ResolveName(playlists, null, today);

        Assert.Equal("Merged Playlist - 2026-02-25", name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void ResolveName_WhitespaceOrNullOverride_FallsBackToPlaylistName(string? override_)
    {
        var playlist = PlaylistFaker.WithName("Road Trip").Generate();

        var name = PlaylistMerger.ResolveName([playlist], override_);

        Assert.Equal("Road Trip", name);
    }
}
