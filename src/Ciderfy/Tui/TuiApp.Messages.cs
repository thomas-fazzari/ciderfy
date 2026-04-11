using Ciderfy.Apple;
using Ciderfy.Matching;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private bool TryHandleMessageError(Exception? error)
    {
        if (error is null)
            return false;

        _state.Phase = TuiTransferPhase.Idle;
        _state.AwaitingUserToken = false;
        HandleTransferError(error);
        return true;
    }

    private void ProcessMessage(TuiMessage msg)
    {
        switch (msg)
        {
            case TickMsg:
                _state.SpinnerTick++;
                _state.CursorVisible = _state.SpinnerTick % 8 < 5; // blinking pattern
                break;

            case AuthDoneMsg m:
                HandleAuthDone(m);
                break;

            case PlaylistFetchedMsg m:
                HandlePlaylistFetched(m);
                break;

            case IsrcProgressMsg m:
                _state.ProgressCurrent = m.Current;
                _state.ProgressTotal = m.Total;
                break;

            case IsrcDoneMsg m:
                HandleIsrcDone(m);
                break;

            case TextProgressMsg m:
                _state.ProgressCurrent = m.Current;
                _state.ProgressTotal = m.Total;
                _state.ProgressLabel = Components.Truncate(
                    $"{m.Track.Artist} - {m.Track.Title}",
                    55
                );
                break;

            case TextDoneMsg m:
                HandleTextDone(m);
                break;

            case PlaylistCreatedMsg m:
                HandlePlaylistCreated(m);
                break;
        }
    }

    private void HandleAuthDone(AuthDoneMsg msg)
    {
        if (TryHandleMessageError(msg.Error))
            return;

        _state.Phase = TuiTransferPhase.Idle;
        _state.AwaitingUserToken = false;

        _logs.Append(LogKind.Success, "Developer token OK");
        if (!msg.NeedsUserToken)
        {
            _logs.Append(LogKind.Success, "User token OK");
            _logs.Append(LogKind.Info, StatusSummary());
            return;
        }

        _state.AwaitingUserToken = true;
        _logs.Append(LogKind.Warning, "User token missing.");
        _logs.Append(LogKind.Info, "1) Open https://music.apple.com and sign in");
        _logs.Append(
            LogKind.Info,
            "2) Open DevTools Console and run: MusicKit.getInstance().musicUserToken"
        );
        _logs.Append(LogKind.Info, "3) Copy the returned string and paste it here");
    }

    private void HandlePlaylistFetched(PlaylistFetchedMsg msg)
    {
        if (TryHandleMessageError(msg.Error))
            return;

        var playlists = msg.Playlists;

        _state.TransferTracks = PlaylistMerger.MergeTracks(playlists);
        _state.PlaylistName = PlaylistMerger.ResolveName(playlists, _state.NextPlaylistName);

        var mergeInfo =
            playlists.Count > 1 ? $"Merged {playlists.Count} playlists - " : string.Empty;

        _logs.Append(
            LogKind.Success,
            $"Fetched \"{_state.PlaylistName}\" ({mergeInfo}{_state.TransferTracks.Count} tracks)"
        );
        _logs.Append(LogKind.Info, "Preview ready. Enter starts transfer, Esc goes back.");

        _state.Phase = TuiTransferPhase.ConfirmPlaylist;
    }

    private void HandleIsrcDone(IsrcDoneMsg msg)
    {
        if (TryHandleMessageError(msg.Error))
            return;

        _state.IsrcResults = msg.Matched;
        _state.UnmatchedTracks = msg.Unmatched;

        _logs.Append(
            LogKind.Success,
            $"{msg.Matched.Count}/{_state.TransferTracks?.Count ?? 0} matched via ISRC"
        );
        if (msg.Unmatched.Count > 0)
            _logs.Append(LogKind.Info, $"{msg.Unmatched.Count} remaining");

        _logs.Append(LogKind.Separator, string.Empty);

        if (msg.Unmatched.Count > 0)
        {
            _state.Phase = TuiTransferPhase.ConfirmTextMatch;
        }
        else
        {
            _state.Phase = TuiTransferPhase.CreatingPlaylist;
            _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token), _cts.Token);
        }
    }

    private void HandleTextDone(TextDoneMsg msg)
    {
        if (TryHandleMessageError(msg.Error))
            return;

        _state.TextResults = msg.Results;

        var textMatched = msg.Results?.Count(r => r is MatchResult.Matched) ?? 0;
        _logs.Append(
            LogKind.Success,
            $"{textMatched}/{_state.UnmatchedTracks?.Count ?? 0} matched via text"
        );
        _logs.Append(LogKind.Separator, string.Empty);

        _state.Phase = TuiTransferPhase.CreatingPlaylist;
        _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token), _cts.Token);
    }

    private void HandlePlaylistCreated(PlaylistCreatedMsg msg)
    {
        if (TryHandleMessageError(msg.Error))
            return;

        if (msg.Result is not { Success: true, PlaylistId: not null and not "" })
        {
            _state.Phase = TuiTransferPhase.Idle;
            var reason = msg.Result switch
            {
                null => "No response from Apple Music",
                { PlaylistId: null or "" } => "Apple Music did not return a playlist ID",
                { Success: false } => "Failed to add tracks to playlist",
                _ => "Unknown error",
            };
            _logs.Append(LogKind.Error, $"Playlist creation failed: {reason}");
            return;
        }

        _logs.Append(LogKind.Success, $"Playlist created: {msg.Result.PlaylistId}");
        _state.NextPlaylistName = null;
        _state.AllResults = msg.AllResults;
        _state.ScrollOffset = 0;
        _state.Phase = TuiTransferPhase.Done;
    }

    private void HandleTransferError(Exception err)
    {
        switch (err)
        {
            case AppleMusicRateLimitException rl:
                var details = rl.RetryAfterSeconds is { } retryAfter
                    ? $" Retry after about {retryAfter}s."
                    : string.Empty;
                _logs.Append(
                    LogKind.Error,
                    $"Apple Music rate limited (429). Transfer stopped.{details}"
                );
                break;

            case AppleMusicUnauthorizedException:
                tokenCache.Clear();
                _logs.Append(
                    LogKind.Error,
                    "Authentication expired. Run /auth to re-authenticate."
                );
                _logs.Append(LogKind.Info, StatusSummary());
                break;

            case OperationCanceledException:
                _logs.Append(LogKind.Warning, "Cancelled.");
                break;

            default:
                _logs.Append(LogKind.Error, $"Error: {err.Message}");
                break;
        }
    }

    private List<MatchResult> BuildAllResults()
    {
        var allResults = new List<MatchResult>();
        var isrcMap = (_state.IsrcResults ?? [])
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);
        var textMap = (_state.TextResults ?? [])
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);

        foreach (var track in _state.TransferTracks ?? [])
        {
            if (isrcMap.TryGetValue(track.SpotifyId, out var isrcMatch))
                allResults.Add(isrcMatch);
            else if (textMap.TryGetValue(track.SpotifyId, out var textMatch))
                allResults.Add(textMatch);
            else
                allResults.Add(new MatchResult.NotFound(track, "Skipped"));
        }

        return allResults;
    }
}
