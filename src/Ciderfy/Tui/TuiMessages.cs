using Ciderfy.Matching;
using Ciderfy.Spotify;

namespace Ciderfy.Tui;

internal enum TuiTransferPhase
{
    Idle = 0,
    FetchingPlaylist = 1,
    ConfirmPlaylist = 2,
    ResolvingIsrc = 3,
    ConfirmTextMatch = 4,
    TextMatching = 5,
    CreatingPlaylist = 6,
    Done = 7,
}

internal abstract record TuiMessage;

internal abstract record TransferMessage(int TransferId) : TuiMessage;

internal sealed record KeyPressedMsg(ConsoleKeyInfo Key) : TuiMessage;

internal sealed record QuitRequestedMsg : TuiMessage;

internal sealed record FatalErrorMsg(Exception Error) : TuiMessage;

internal sealed record AuthFailedMsg(Exception Error) : TuiMessage;

internal sealed record TransferFailedMsg(int TransferId, Exception Error)
    : TransferMessage(TransferId);

internal sealed record PlaylistFetchedMsg(int TransferId, List<SpotifyPlaylist> Playlists)
    : TransferMessage(TransferId);

internal sealed record IsrcProgressMsg(int TransferId, int Current, int Total)
    : TransferMessage(TransferId);

internal sealed record IsrcDoneMsg(
    int TransferId,
    List<MatchResult.Matched> Matched,
    List<TrackMetadata> Unmatched
) : TransferMessage(TransferId);

internal sealed record TextProgressMsg(int TransferId, TrackMetadata Track, int Current, int Total)
    : TransferMessage(TransferId);

internal sealed record TextDoneMsg(int TransferId, List<MatchResult> Results)
    : TransferMessage(TransferId);

internal sealed record PlaylistCreatedMsg(
    int TransferId,
    PlaylistCreateResult Result,
    List<MatchResult> AllResults
) : TransferMessage(TransferId);

internal sealed record AuthDoneMsg(bool NeedsUserToken) : TuiMessage;
