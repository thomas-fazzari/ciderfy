using Ciderfy.Matching;

namespace Ciderfy.Tui;

internal sealed class TuiState
{
    internal TuiTransferPhase Phase { get; set; } = TuiTransferPhase.Idle;
    internal string Storefront { get; set; } = "us";
    internal string? NextPlaylistName { get; set; }
    internal bool AwaitingUserToken { get; set; }
    internal bool ShowHelp { get; set; }
    internal bool QuitRequested { get; set; }
    internal List<string> QueuedPlaylistUrls { get; } = [];
    internal int SpinnerTick { get; set; }
    internal int ProgressCurrent { get; set; }
    internal int ProgressTotal { get; set; }
    internal string ProgressLabel { get; set; } = string.Empty;
    internal List<TrackMetadata>? TransferTracks { get; set; }
    internal List<MatchResult.Matched>? IsrcResults { get; set; }
    internal List<TrackMetadata>? UnmatchedTracks { get; set; }
    internal List<MatchResult>? TextResults { get; set; }
    internal string PlaylistName { get; set; } = string.Empty;
    internal List<MatchResult>? AllResults { get; set; }
    internal int ScrollOffset { get; set; }
    internal bool CursorVisible { get; set; } = true;

    internal void ResetTransferState()
    {
        Phase = TuiTransferPhase.Idle;
        ProgressCurrent = 0;
        ProgressTotal = 0;
        ProgressLabel = string.Empty;
        TransferTracks = null;
        IsrcResults = null;
        UnmatchedTracks = null;
        TextResults = null;
        AllResults = null;
        PlaylistName = string.Empty;
        ScrollOffset = 0;
    }
}
