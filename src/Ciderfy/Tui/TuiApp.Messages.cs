using Ciderfy.Apple;
using Ciderfy.Matching;

namespace Ciderfy.Tui;

internal sealed partial class TuiApp
{
    private void ProcessMessage(TuiMessage msg)
    {
        switch (msg)
        {
            case TickMsg:
                _spinnerTick++;
                _cursorVisible = _spinnerTick % 8 < 5; // blinking pattern
                break;

            case AuthDoneMsg m:
                HandleAuthDone(m);
                break;

            case PlaylistFetchedMsg m:
                HandlePlaylistFetched(m);
                break;

            case IsrcProgressMsg m:
                _progressCurrent = m.Current;
                _progressTotal = m.Total;
                break;

            case IsrcDoneMsg m:
                HandleIsrcDone(m);
                break;

            case TextProgressMsg m:
                _progressCurrent = m.Current;
                _progressTotal = m.Total;
                _progressLabel = Components.Truncate($"{m.Track.Artist} - {m.Track.Title}", 55);
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
        _phase = TuiTransferPhase.Idle;
        if (msg.Error is not null)
        {
            HandleTransferError(msg.Error);
            return;
        }

        _logs.Append(LogKind.Success, "Developer token OK");
        if (!msg.NeedsUserToken)
        {
            _logs.Append(LogKind.Success, "User token OK");
            _logs.Append(LogKind.Info, StatusSummary());
            return;
        }

        _awaitingUserToken = true;
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
        if (msg.Error is not null)
        {
            _phase = TuiTransferPhase.Idle;
            HandleTransferError(msg.Error);
            return;
        }

        var playlist = msg.Playlist!;
        _transferTracks =
        [
            .. playlist.Tracks.Select(t => new TrackMetadata
            {
                SpotifyId = t.SpotifyId,
                Title = t.Title,
                Artist = t.Artist,
                DurationMs = t.DurationMs,
            }),
        ];

        _playlistName = string.IsNullOrWhiteSpace(_nextPlaylistName)
            ? playlist.Name
            : _nextPlaylistName;
        if (string.IsNullOrWhiteSpace(_playlistName))
            _playlistName = "Spotify Import";

        _logs.Append(
            LogKind.Success,
            $"Fetched \"{_playlistName}\" ({_transferTracks.Count} tracks)"
        );
        _logs.Append(LogKind.Info, "Preview ready. Enter starts transfer, Esc goes back.");

        _phase = TuiTransferPhase.ConfirmPlaylist;
    }

    private void HandleIsrcDone(IsrcDoneMsg msg)
    {
        if (msg.Error is not null)
        {
            _phase = TuiTransferPhase.Idle;
            HandleTransferError(msg.Error);
            return;
        }

        _isrcResults = msg.Matched;
        _unmatchedTracks = msg.Unmatched;

        _logs.Append(
            LogKind.Success,
            $"{msg.Matched.Count}/{_transferTracks?.Count ?? 0} matched via ISRC"
        );
        if (msg.Unmatched.Count > 0)
            _logs.Append(LogKind.Info, $"{msg.Unmatched.Count} remaining");

        if (msg.Unmatched.Count > 0)
        {
            _phase = TuiTransferPhase.ConfirmTextMatch;
        }
        else
        {
            _phase = TuiTransferPhase.CreatingPlaylist;
            _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token));
        }
    }

    private void HandleTextDone(TextDoneMsg msg)
    {
        if (msg.Error is not null)
        {
            _phase = TuiTransferPhase.Idle;
            HandleTransferError(msg.Error);
            return;
        }

        _textResults = msg.Results;

        var textMatched = msg.Results?.Count(r => r is MatchResult.Matched) ?? 0;
        _logs.Append(
            LogKind.Success,
            $"{textMatched}/{_unmatchedTracks?.Count ?? 0} matched via text"
        );

        _phase = TuiTransferPhase.CreatingPlaylist;
        _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token));
    }

    private void HandlePlaylistCreated(PlaylistCreatedMsg msg)
    {
        if (msg.Error is not null)
        {
            _phase = TuiTransferPhase.Idle;
            HandleTransferError(msg.Error);
            return;
        }

        if (msg.Result is not { Success: true } || string.IsNullOrWhiteSpace(msg.Result.PlaylistId))
        {
            _phase = TuiTransferPhase.Idle;
            _logs.Append(LogKind.Error, "Playlist creation failed.");
            return;
        }

        _logs.Append(LogKind.Success, $"Playlist created: {msg.Result.PlaylistId}");
        _nextPlaylistName = null;
        _allResults = BuildAllResults();
        _scrollOffset = 0;
        _phase = TuiTransferPhase.Done;
    }

    private void HandleTransferError(Exception err)
    {
        switch (err)
        {
            case AppleMusicRateLimitException rl:
                var details = rl.RetryAfterSeconds is { } retryAfter
                    ? $" Retry after about {retryAfter}s."
                    : "";
                _logs.Append(
                    LogKind.Error,
                    $"Apple Music rate limited (429). Transfer stopped.{details}"
                );
                break;

            case AppleMusicUnauthorizedException:
                tokenCache.ClearDeveloperToken();
                _logs.Append(LogKind.Error, "Developer token expired. Run /auth to refresh it.");
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
        var isrcMap = (_isrcResults ?? [])
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);
        var textMap = (_textResults ?? [])
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);

        foreach (var track in _transferTracks ?? [])
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
