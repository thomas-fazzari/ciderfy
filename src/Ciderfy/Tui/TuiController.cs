using System.Text;
using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Spotify;

namespace Ciderfy.Tui;

internal sealed class TuiController(
    TokenCache tokenCache,
    Func<int> getVisibleHelpRows,
    Func<int> getVisibleDoneRows
)
{
    private string? _selectedCommandSuggestionCompletion;
    private int _transferId;

    internal LogBuffer Logs { get; } = new();
    internal StringBuilder InputBuffer { get; } = new();
    internal TuiState State { get; } = new();

    internal IReadOnlyList<TuiCommandSuggestion> CommandSuggestions =>
        TuiCommands.GetSuggestions(InputBuffer.ToString(), State.AwaitingUserToken);

    internal int SelectedCommandSuggestionIndex =>
        GetSelectedCommandSuggestionIndex(CommandSuggestions);

    internal IReadOnlyList<ITuiEffect> ProcessMessage(TuiMessage msg)
    {
        var effects = new List<ITuiEffect>();

        if (msg is TransferMessage { TransferId: var transferId } && transferId != _transferId)
        {
            return effects;
        }

        switch (msg)
        {
            case KeyPressedMsg m:
                HandleKey(m.Key, effects);
                break;
            case QuitRequestedMsg:
                RequestQuit(effects);
                break;
            case FatalErrorMsg m:
                HandleFatalError(m.Error, effects);
                break;
            case AuthFailedMsg m:
                HandleAuthFailed(m.Error);
                break;
            case TransferFailedMsg m:
                HandleTransferFailed(m.Error, effects);
                break;
            case AuthDoneMsg m:
                HandleAuthDone(m);
                break;
            case ConfigOpenedMsg m:
                Logs.Append(LogKind.Success, $"Opened config folder: {m.Directory}");
                break;
            case ConfigOpenFailedMsg m:
                Logs.Append(LogKind.Error, $"Could not open config folder: {m.Message}");
                break;
            case PlaylistFetchedMsg m:
                HandlePlaylistFetched(m);
                break;
            case IsrcProgressMsg m:
                State.ProgressCurrent = m.Current;
                State.ProgressTotal = m.Total;
                break;
            case IsrcDoneMsg m:
                HandleIsrcDone(m, effects);
                break;
            case TextProgressMsg m:
                State.ProgressCurrent = m.Current;
                State.ProgressTotal = m.Total;
                State.ProgressLabel = RenderHelpers.Truncate(
                    $"{m.Track.Artist} - {m.Track.Title}",
                    55
                );
                break;
            case TextDoneMsg m:
                HandleTextDone(m, effects);
                break;
            case PlaylistCreatedMsg m:
                HandlePlaylistCreated(m);
                break;
        }

        return effects;
    }

    internal string StatusSummary()
    {
        var dev = tokenCache.HasValidDeveloperToken ? "valid" : "missing";
        var user = tokenCache.HasValidUserToken ? "valid" : "missing";
        return $"Developer token: {dev}  |  User token: {user}  |  Storefront: {State.Storefront}";
    }

    private void HandleKey(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        if (HandleQuitShortcut(key, effects))
        {
            return;
        }

        if (TryHandlePhaseInput(key, effects) || State.Phase is not TuiTransferPhase.Idle)
        {
            return;
        }

        HandleGeneralInput(key, effects);
    }

    private bool TryHandlePhaseInput(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        if (TryHandleTransferCancelInput(key, effects))
        {
            return true;
        }

        return State is { ShowHelp: true, Phase: TuiTransferPhase.Idle }
            ? TryHandleHelpInput(key)
            : State.Phase switch
            {
                TuiTransferPhase.ConfirmPlaylist => TryHandlePlaylistConfirmInput(key, effects),
                TuiTransferPhase.ConfirmTextMatch => HandleConfirmKey(key, effects),
                TuiTransferPhase.Done => TryHandleDonePhaseInput(key),
                _ => false,
            };
    }

    private bool TryHandleTransferCancelInput(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        if (key.Key is not ConsoleKey.Escape)
        {
            return false;
        }

        if (
            State.Phase
            is not (
                TuiTransferPhase.FetchingPlaylist
                or TuiTransferPhase.ConfirmPlaylist
                or TuiTransferPhase.ResolvingIsrc
                or TuiTransferPhase.ConfirmTextMatch
                or TuiTransferPhase.TextMatching
                or TuiTransferPhase.CreatingPlaylist
            )
        )
        {
            return false;
        }

        CancelTransfer(effects);
        return true;
    }

    private bool TryHandleHelpInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                State.ScrollOffset = Math.Max(0, State.ScrollOffset - 1);
                return true;
            case ConsoleKey.DownArrow:
                ScrollOffsetDown(HelpSection.EntryCount, getVisibleHelpRows());
                return true;
            case ConsoleKey.Enter:
                State.ShowHelp = false;
                State.ScrollOffset = 0;
                return true;
            default:
                return true;
        }
    }

    private bool HandleQuitShortcut(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        if (key is not { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control })
        {
            return false;
        }

        RequestQuit(effects);
        return true;
    }

    private bool TryHandlePlaylistConfirmInput(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        if (key.Key is ConsoleKey.Enter)
        {
            if (State.TransferTracks.Count == 0)
            {
                return true;
            }

            var tracks = State.TransferTracks.ToList();
            var storefront = State.Storefront;

            State.Phase = TuiTransferPhase.ResolvingIsrc;
            State.ProgressCurrent = 0;
            State.ProgressTotal = tracks.Count;
            Logs.Append(LogKind.Info, "Starting ISRC matching...");
            effects.Add(new StartIsrcMatchEffect(_transferId, tracks, storefront));
        }
        else if (key.Key is ConsoleKey.Escape or ConsoleKey.Backspace)
        {
            CancelTransfer(effects);
        }

        return true;
    }

    private bool TryHandleDonePhaseInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                State.ScrollOffset = Math.Max(0, State.ScrollOffset - 1);
                return true;
            case ConsoleKey.DownArrow:
                ScrollOffsetDown(State.AllResults.Count, getVisibleDoneRows());
                return true;
            case ConsoleKey.Enter:
                State.ResetTransferState();
                return true;
            default:
                return false;
        }
    }

    private void ScrollOffsetDown(int totalCount, int visibleRows)
    {
        State.ScrollOffset = Math.Min(
            Math.Max(0, totalCount - visibleRows),
            State.ScrollOffset + 1
        );
    }

    private void HandleGeneralInput(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                if (TryCompleteCommandSuggestion())
                {
                    break;
                }

                HandleEnter(effects);
                break;
            case ConsoleKey.Tab:
                CompleteCommandSuggestion();
                break;
            case ConsoleKey.UpArrow:
                MoveCommandSuggestionSelection(-1);
                break;
            case ConsoleKey.DownArrow:
                MoveCommandSuggestionSelection(1);
                break;
            case ConsoleKey.Backspace:
                if (InputBuffer.Length > 0)
                {
                    InputBuffer.Remove(InputBuffer.Length - 1, 1);
                }

                ClampCommandSuggestionSelection();
                break;
            case ConsoleKey.Escape:
                HandleEscape();
                break;
            default:
                if (key.KeyChar >= 32)
                {
                    InputBuffer.Append(key.KeyChar);
                    ClampCommandSuggestionSelection();
                }
                break;
        }
    }

    private void MoveCommandSuggestionSelection(int delta)
    {
        var suggestions = CommandSuggestions;
        if (suggestions.Count == 0)
        {
            _selectedCommandSuggestionCompletion = null;
            return;
        }

        var selectedIndex = GetSelectedCommandSuggestionIndex(suggestions);
        var nextIndex = Math.Clamp(selectedIndex + delta, 0, suggestions.Count - 1);
        _selectedCommandSuggestionCompletion = suggestions[nextIndex].Completion;
    }

    private void ClampCommandSuggestionSelection()
    {
        var suggestions = CommandSuggestions;
        if (suggestions.Count == 0)
        {
            _selectedCommandSuggestionCompletion = null;
            return;
        }

        _selectedCommandSuggestionCompletion = suggestions[
            GetSelectedCommandSuggestionIndex(suggestions)
        ].Completion;
    }

    private bool TryCompleteCommandSuggestion()
    {
        var suggestions = CommandSuggestions;
        if (suggestions.Count == 0)
        {
            return false;
        }

        var input = InputBuffer.ToString();
        if (suggestions.Any(s => s.Completion.Equals(input, StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        var selectedSuggestion = suggestions[GetSelectedCommandSuggestionIndex(suggestions)];
        CompleteCommandSuggestion(selectedSuggestion);
        return true;
    }

    private void CompleteCommandSuggestion()
    {
        var suggestions = CommandSuggestions;
        if (suggestions.Count == 0)
        {
            return;
        }

        CompleteCommandSuggestion(suggestions[GetSelectedCommandSuggestionIndex(suggestions)]);
    }

    private void CompleteCommandSuggestion(TuiCommandSuggestion selectedSuggestion)
    {
        InputBuffer.Clear().Append(selectedSuggestion.Completion);
        _selectedCommandSuggestionCompletion = selectedSuggestion.Completion;
        ClampCommandSuggestionSelection();
    }

    private int GetSelectedCommandSuggestionIndex(IReadOnlyList<TuiCommandSuggestion> suggestions)
    {
        if (suggestions.Count == 0)
        {
            return 0;
        }

        for (var i = 0; i < suggestions.Count; i++)
        {
            if (suggestions[i].Completion == _selectedCommandSuggestionCompletion)
            {
                return i;
            }
        }

        return GetDefaultCommandSuggestionIndex(suggestions);
    }

    private int GetDefaultCommandSuggestionIndex(IReadOnlyList<TuiCommandSuggestion> suggestions)
    {
        var input = InputBuffer.ToString();
        for (var i = 0; i < suggestions.Count; i++)
        {
            if (suggestions[i].Completion.Equals(input, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return 0;
    }

    private void HandleEscape()
    {
        InputBuffer.Clear();
        _selectedCommandSuggestionCompletion = null;
        if (!State.AwaitingUserToken)
        {
            return;
        }

        State.AwaitingUserToken = false;
        Logs.Append(LogKind.Info, "Authentication cancelled.");
    }

    private bool HandleConfirmKey(ConsoleKeyInfo key, List<ITuiEffect> effects)
    {
        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'y' or '\r' or '\n':
                if (State.UnmatchedTracks.Count == 0)
                {
                    return true;
                }

                var unmatchedTracks = State.UnmatchedTracks.ToList();
                var storefront = State.Storefront;

                State.Phase = TuiTransferPhase.TextMatching;
                State.ProgressCurrent = 0;
                State.ProgressTotal = unmatchedTracks.Count;
                State.ProgressLabel = string.Empty;
                Logs.Append(LogKind.Info, "Starting text matching...");
                effects.Add(new StartTextMatchEffect(_transferId, unmatchedTracks, storefront));
                break;
            case 'n':
                Logs.Append(LogKind.Info, "Text matching skipped.");
                if (State.UnmatchedTracks.Count > 0)
                {
                    foreach (var t in State.UnmatchedTracks)
                    {
                        State.TextResults.Add(new MatchResult.NotFound(t, "Skipped"));
                    }
                }

                State.Phase = TuiTransferPhase.CreatingPlaylist;
                StartCreatePlaylist(_transferId, effects);
                break;
        }

        return true;
    }

    private void HandleEnter(List<ITuiEffect> effects)
    {
        State.ShowHelp = false;

        var raw = InputBuffer.ToString();
        InputBuffer.Clear();
        _selectedCommandSuggestionCompletion = null;

        if (State.AwaitingUserToken)
        {
            HandleUserTokenInput(raw);
            return;
        }

        raw = raw.Trim();

        if (string.IsNullOrEmpty(raw))
        {
            return;
        }

        if (raw.StartsWith('/'))
        {
            HandleCommand(raw, effects);
            return;
        }

        if (SpotifyUrlInfo.TryParse(raw, out var urlInfo))
        {
            if (State.QueuedPlaylistUrls.Count > 0)
            {
                State.QueuedPlaylistUrls.Add(urlInfo.Id);
                Logs.Append(
                    LogKind.Success,
                    $"Added to merge queue (Total: {State.QueuedPlaylistUrls.Count}). Type /run to start."
                );
                return;
            }

            StartPlaylistFetch([urlInfo.Id], "Starting transfer...", effects);
            return;
        }

        Logs.Append(LogKind.Error, "Not a valid command or Spotify URL. Type /help");
    }

    private void HandleUserTokenInput(string raw)
    {
        var trimmed = raw.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            Logs.Append(LogKind.Error, "No token provided.");
            return;
        }

        tokenCache.UserToken = trimmed;
        tokenCache.UserTokenExpiry = DateTimeOffset.UtcNow.AddMonths(6);
        tokenCache.Save();

        State.AwaitingUserToken = false;
        Logs.Append(LogKind.Success, "Authentication complete! Tokens cached.");
        Logs.Append(LogKind.Info, StatusSummary());
    }

    private void HandleCommand(string raw, List<ITuiEffect> effects)
    {
        var command = TuiCommands.Parse(raw);

        switch (command.Kind)
        {
            case TuiCommandKind.Quit:
                RequestQuit(effects);
                break;
            case TuiCommandKind.Help:
                ToggleHelp();
                break;
            case TuiCommandKind.Status:
                Logs.Append(LogKind.Info, StatusSummary());
                break;
            case TuiCommandKind.Storefront:
                HandleStorefrontCommand(command.Argument);
                break;
            case TuiCommandKind.Name:
                HandleNameCommand(command.Argument);
                break;
            case TuiCommandKind.Auth:
                HandleAuthCommand(resetTokens: false, effects: effects);
                break;
            case TuiCommandKind.AuthReset:
                HandleAuthCommand(resetTokens: true, effects: effects);
                break;
            case TuiCommandKind.Config:
                effects.Add(new OpenConfigEffect());
                break;
            case TuiCommandKind.Add:
                HandleAddCommand(command.Argument);
                break;
            case TuiCommandKind.Run:
                HandleRunCommand(effects);
                break;
            default:
                Logs.Append(LogKind.Error, $"Unknown command: {command.Name}. Type /help");
                break;
        }
    }

    private void ToggleHelp()
    {
        State.ShowHelp = !State.ShowHelp;
        State.ScrollOffset = 0;
    }

    private void HandleStorefrontCommand(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            Logs.Append(LogKind.Info, $"Storefront: {State.Storefront}. Usage: /storefront fr");
            return;
        }

#pragma warning disable CA1308
        State.Storefront = argument.ToLowerInvariant();
#pragma warning restore CA1308
        Logs.Append(LogKind.Success, $"Storefront set to {State.Storefront}");
    }

    private void HandleNameCommand(string? argument)
    {
        if (string.IsNullOrEmpty(argument))
        {
            State.NextPlaylistName = null;
            Logs.Append(LogKind.Success, "Playlist name override cleared");
            return;
        }

        State.NextPlaylistName = argument;
        Logs.Append(LogKind.Success, $"Next playlist name set to \"{State.NextPlaylistName}\"");
    }

    private void HandleAuthCommand(bool resetTokens, List<ITuiEffect> effects)
    {
        Logs.Clear();

        if (resetTokens)
        {
            tokenCache.Clear();
            Logs.Append(LogKind.Warning, "Tokens cleared.");
        }

        Logs.Append(LogKind.Info, "Authenticating...");
        effects.Add(new StartAuthEffect());
    }

    private void HandleAddCommand(string? argument)
    {
        if (string.IsNullOrWhiteSpace(argument))
        {
            Logs.Append(LogKind.Error, "Usage: /add <url1> [url2] ...");
            return;
        }

        var urls = argument.Split(
            ' ',
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries
        );
        var addedCount = 0;

        foreach (var url in urls)
        {
            if (
                !SpotifyUrlInfo.TryParse(url, out var urlInfo)
                || State.QueuedPlaylistUrls.Contains(urlInfo.Id)
            )
            {
                continue;
            }

            State.QueuedPlaylistUrls.Add(urlInfo.Id);
            addedCount++;
        }

        if (addedCount > 0)
        {
            Logs.Append(
                LogKind.Success,
                $"Added {addedCount} playlists to queue. ({State.QueuedPlaylistUrls.Count} total)"
            );
            Logs.Append(LogKind.Info, "Type /run to start merging.");
        }
        else
        {
            Logs.Append(LogKind.Error, "No valid Spotify URLs found.");
        }
    }

    private void HandleRunCommand(List<ITuiEffect> effects)
    {
        if (State.QueuedPlaylistUrls.Count == 0)
        {
            Logs.Append(LogKind.Error, "Queue is empty. Add playlists with /add <url>");
            return;
        }

        var playlistIdsToFetch = State.QueuedPlaylistUrls.ToList();
        State.QueuedPlaylistUrls.Clear();

        StartPlaylistFetch(
            playlistIdsToFetch,
            $"Starting transfer of {playlistIdsToFetch.Count} merged playlists...",
            effects
        );
    }

    private void HandleAuthDone(AuthDoneMsg msg)
    {
        State.Phase = TuiTransferPhase.Idle;
        State.AwaitingUserToken = false;

        Logs.Append(LogKind.Success, "Developer token OK");
        if (!msg.NeedsUserToken)
        {
            Logs.Append(LogKind.Success, "User token OK");
            Logs.Append(LogKind.Info, StatusSummary());
            return;
        }

        State.AwaitingUserToken = true;
        Logs.Append(LogKind.Warning, "User token missing.");
        Logs.Append(LogKind.Info, "1) Open https://music.apple.com and sign in");
        Logs.Append(
            LogKind.Info,
            "2) Open DevTools Console and run: MusicKit.getInstance().musicUserToken"
        );
        Logs.Append(LogKind.Info, "3) Copy the returned string and paste it here");
    }

    private void HandlePlaylistFetched(PlaylistFetchedMsg msg)
    {
        var playlists = msg.Playlists;

        State.TransferTracks = PlaylistMerger.MergeTracks(playlists);
        State.PlaylistName = PlaylistMerger.ResolveName(playlists, State.NextPlaylistName);

        var mergeInfo =
            playlists.Count > 1 ? $"Merged {playlists.Count} playlists - " : string.Empty;

        Logs.Append(
            LogKind.Success,
            $"Fetched \"{State.PlaylistName}\" ({mergeInfo}{State.TransferTracks.Count} tracks)"
        );
        Logs.Append(LogKind.Info, "Preview ready. Enter starts transfer, Esc goes back.");

        State.Phase = TuiTransferPhase.ConfirmPlaylist;
    }

    private void HandleIsrcDone(IsrcDoneMsg msg, List<ITuiEffect> effects)
    {
        State.IsrcResults = msg.Matched;
        State.UnmatchedTracks = msg.Unmatched;

        Logs.Append(
            LogKind.Success,
            $"{msg.Matched.Count}/{State.TransferTracks.Count} matched via ISRC"
        );
        if (msg.Unmatched.Count > 0)
        {
            Logs.Append(LogKind.Info, $"{msg.Unmatched.Count} remaining");
        }

        Logs.Append(LogKind.Separator, string.Empty);

        if (msg.Unmatched.Count > 0)
        {
            State.Phase = TuiTransferPhase.ConfirmTextMatch;
        }
        else
        {
            State.Phase = TuiTransferPhase.CreatingPlaylist;
            StartCreatePlaylist(msg.TransferId, effects);
        }
    }

    private void HandleTextDone(TextDoneMsg msg, List<ITuiEffect> effects)
    {
        State.TextResults = msg.Results;

        var textMatched = msg.Results.Count(r => r is MatchResult.Matched);
        Logs.Append(
            LogKind.Success,
            $"{textMatched}/{State.UnmatchedTracks.Count} matched via text"
        );
        Logs.Append(LogKind.Separator, string.Empty);

        State.Phase = TuiTransferPhase.CreatingPlaylist;
        StartCreatePlaylist(msg.TransferId, effects);
    }

    private void HandlePlaylistCreated(PlaylistCreatedMsg msg)
    {
        if (msg.Result is not { Success: true, PlaylistId: not null and not "" })
        {
            State.Phase = TuiTransferPhase.Idle;
            var reason = msg.Result switch
            {
                { PlaylistId: null or "" } => "Apple Music did not return a playlist ID",
                { Success: false } => "Failed to add tracks to playlist",
                _ => "Unknown error",
            };
            Logs.Append(LogKind.Error, $"Playlist creation failed: {reason}");
            return;
        }

        Logs.Append(LogKind.Success, $"Playlist created: {msg.Result.PlaylistId}");
        State.NextPlaylistName = null;
        State.AllResults = msg.AllResults;
        State.ScrollOffset = 0;
        State.Phase = TuiTransferPhase.Done;
    }

    private void HandleAuthFailed(Exception err)
    {
        State.Phase = TuiTransferPhase.Idle;
        State.AwaitingUserToken = false;
        HandleTransferError(err);
    }

    private void HandleTransferFailed(Exception err, List<ITuiEffect> effects)
    {
        _transferId++;
        effects.Add(new CancelCurrentTransferEffect());
        State.Phase = TuiTransferPhase.Idle;
        State.AwaitingUserToken = false;
        HandleTransferError(err);
    }

    private void HandleTransferError(Exception err)
    {
        switch (err)
        {
            case AppleMusicRateLimitException rl:
                var details = rl.RetryAfterSeconds is { } retryAfter
                    ? $" Retry after about {retryAfter}s."
                    : string.Empty;
                Logs.Append(
                    LogKind.Error,
                    $"Apple Music rate limited (429). Transfer stopped.{details}"
                );
                break;

            case AppleMusicUnauthorizedException:
                tokenCache.Clear();
                Logs.Append(LogKind.Error, "Authentication expired. Run /auth to re-authenticate.");
                Logs.Append(LogKind.Info, StatusSummary());
                break;

            case OperationCanceledException:
                Logs.Append(LogKind.Warning, "Cancelled.");
                break;

            default:
                Logs.Append(LogKind.Error, $"Error: {err.Message}");
                break;
        }
    }

    private void HandleFatalError(Exception err, List<ITuiEffect> effects)
    {
        _transferId++;
        effects.Add(new CancelCurrentTransferEffect());
        State.Phase = TuiTransferPhase.Idle;
        State.AwaitingUserToken = false;
        Logs.Append(LogKind.Error, $"Fatal background error: {err.Message}");
    }

    private void RequestQuit(List<ITuiEffect> effects)
    {
        State.QuitRequested = true;
        effects.Add(new QuitAppEffect());
    }

    private void CancelTransfer(List<ITuiEffect> effects)
    {
        _transferId++;
        effects.Add(new CancelCurrentTransferEffect());
        State.ResetTransferState();
        Logs.Append(LogKind.Info, "Transfer cancelled.");
    }

    private void StartPlaylistFetch(
        IReadOnlyCollection<string> playlistIds,
        string startMessage,
        List<ITuiEffect> effects
    )
    {
        var transferId = ++_transferId;
        State.ResetTransferState();
        State.Phase = TuiTransferPhase.FetchingPlaylist;
        Logs.Clear();
        Logs.Append(LogKind.Info, startMessage);
        effects.Add(new StartFetchPlaylistsEffect(transferId, playlistIds.ToArray()));
    }

    private void StartCreatePlaylist(int transferId, List<ITuiEffect> effects)
    {
        var allResults = BuildAllResults();
        var playlistName = State.PlaylistName;
        effects.Add(new StartCreatePlaylistEffect(transferId, playlistName, allResults));
    }

    private List<MatchResult> BuildAllResults()
    {
        var allResults = new List<MatchResult>();
        var isrcMap = State
            .IsrcResults.DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);
        var textMap = State
            .TextResults.DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);

        foreach (var track in State.TransferTracks)
        {
            if (isrcMap.TryGetValue(track.SpotifyId, out var isrcMatch))
            {
                allResults.Add(isrcMatch);
            }
            else if (textMap.TryGetValue(track.SpotifyId, out var textMatch))
            {
                allResults.Add(textMatch);
            }
            else
            {
                allResults.Add(new MatchResult.NotFound(track, "Skipped"));
            }
        }

        return allResults;
    }
}
