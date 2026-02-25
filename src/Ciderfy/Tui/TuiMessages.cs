using Ciderfy.Matching;
using Ciderfy.Spotify;

namespace Ciderfy.Tui;

/// <summary>
/// Tracks which stage of the playlist transfer pipeline the TUI is in
/// </summary>
internal enum TuiTransferPhase
{
    Idle,
    FetchingPlaylist,
    ConfirmPlaylist,
    ResolvingIsrc,
    ConfirmTextMatch,
    TextMatching,
    CreatingPlaylist,
    Done,
}

/// <summary>
/// Base type for messages posted to the TUI channel by background tasks
/// </summary>
internal abstract record TuiMessage;

/// <summary>
/// Signals that a Spotify playlist fetch completed or failed
/// </summary>
internal sealed record PlaylistFetchedMsg(SpotifyPlaylist? Playlist, Exception? Error) : TuiMessage;

/// <summary>
/// Reports ISRC matching progress to update the progress bar
/// </summary>
internal sealed record IsrcProgressMsg(int Current, int Total) : TuiMessage;

/// <summary>
/// Signals that ISRC matching completed with matched and unmatched track lists
/// </summary>
internal sealed record IsrcDoneMsg(
    List<MatchResult.Matched> Matched,
    List<TrackMetadata> Unmatched,
    Exception? Error
) : TuiMessage;

/// <summary>
/// Reports text matching progress for a single track
/// </summary>
internal sealed record TextProgressMsg(TrackMetadata Track, int Current, int Total) : TuiMessage;

/// <summary>
/// Signals that text-based matching completed or failed
/// </summary>
internal sealed record TextDoneMsg(List<MatchResult>? Results, Exception? Error) : TuiMessage;

/// <summary>
/// Signals that Apple Music playlist creation completed or failed
/// </summary>
internal sealed record PlaylistCreatedMsg(PlaylistCreateResult? Result, Exception? Error)
    : TuiMessage;

/// <summary>
/// Signals that Apple Music authentication completed or failed
/// </summary>
internal sealed record AuthDoneMsg(bool NeedsUserToken, Exception? Error) : TuiMessage;

/// <summary>
/// Periodic tick for spinner animation and cursor blink
/// </summary>
internal sealed record TickMsg : TuiMessage;
