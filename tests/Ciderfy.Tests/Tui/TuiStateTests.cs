using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Tui;
using Xunit;

namespace Ciderfy.Tests;

public sealed class TuiStateTests
{
    [Fact]
    public void Collections_Default_To_Empty()
    {
        var state = new TuiState();

        Assert.Empty(state.TransferTracks);
        Assert.Empty(state.IsrcResults);
        Assert.Empty(state.UnmatchedTracks);
        Assert.Empty(state.TextResults);
        Assert.Empty(state.AllResults);
    }

    [Fact]
    public void ResetTransferState_Clears_Collections_And_Resets_Phase()
    {
        var track = new TrackMetadata
        {
            SpotifyId = "abc",
            Title = "Song",
            Artist = "Artist",
            DurationMs = 180_000,
        };
        var state = new TuiState
        {
            Phase = TuiTransferPhase.Done,
            TransferTracks = [track],
            IsrcResults =
            [
                new MatchResult.Matched(
                    track,
                    new AppleMusicTrack
                    {
                        Id = "x",
                        Title = "Song",
                        Artist = "Artist",
                        DurationMs = 180_000,
                    },
                    MatchMethod.Isrc,
                    1.0
                ),
            ],
            UnmatchedTracks = [track],
            TextResults = [new MatchResult.NotFound(track, "Skipped")],
            AllResults = [new MatchResult.NotFound(track, "Skipped")],
            PlaylistName = "My Playlist",
            ProgressCurrent = 5,
            ProgressTotal = 10,
            ProgressLabel = "test",
            ScrollOffset = 3,
        };

        state.ResetTransferState();

        Assert.Equal(TuiTransferPhase.Idle, state.Phase);
        Assert.Empty(state.TransferTracks);
        Assert.Empty(state.IsrcResults);
        Assert.Empty(state.UnmatchedTracks);
        Assert.Empty(state.TextResults);
        Assert.Empty(state.AllResults);
        Assert.Equal(string.Empty, state.PlaylistName);
        Assert.Equal(0, state.ProgressCurrent);
        Assert.Equal(0, state.ProgressTotal);
        Assert.Equal(string.Empty, state.ProgressLabel);
        Assert.Equal(0, state.ScrollOffset);
    }

    [Fact]
    public void ResetTransferState_Preserves_NonTransfer_Fields()
    {
        var state = new TuiState
        {
            Storefront = "fr",
            ShowHelp = true,
            QuitRequested = true,
            AwaitingUserToken = true,
            NextPlaylistName = "override",
        };
        state.QueuedPlaylistUrls.Add("spotify:playlist:abc");

        state.ResetTransferState();

        Assert.Equal("fr", state.Storefront);
        Assert.True(state.ShowHelp);
        Assert.True(state.QuitRequested);
        Assert.True(state.AwaitingUserToken);
        Assert.Equal("override", state.NextPlaylistName);
        Assert.Equal(["spotify:playlist:abc"], state.QueuedPlaylistUrls);
    }

    [Fact]
    public void ResetTransferState_Idempotent()
    {
        var state = new TuiState();
        state.ResetTransferState();

        Assert.Equal(TuiTransferPhase.Idle, state.Phase);
        Assert.Empty(state.TransferTracks);
    }
}
