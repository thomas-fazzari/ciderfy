using System.Text;
using System.Threading.Channels;
using Ciderfy.Apple;
using Ciderfy.Configuration;
using Ciderfy.Matching;
using Ciderfy.Spotify;

namespace Ciderfy.Tui;

internal sealed class TuiController(
    TokenCache tokenCache,
    ChannelWriter<TuiMessage> messages,
    Action cancelApp,
    Action startAuth,
    Func<int> getVisibleHelpRows,
    Func<int> getVisibleDoneRows,
    Func<int, IReadOnlyCollection<string>, CancellationToken, Task> fetchPlaylists,
    Func<int, List<TrackMetadata>, string, CancellationToken, Task> matchByIsrc,
    Func<int, List<TrackMetadata>, string, CancellationToken, Task> matchByText,
    Func<int, string, List<MatchResult>, CancellationToken, Task> createPlaylist,
    CancellationToken appToken
) : IDisposable
{
    private readonly Dictionary<string, Action<string?>> _commands = new(
        StringComparer.OrdinalIgnoreCase
    );
    private string? _selectedCommandSuggestionCompletion;

    internal LogBuffer Logs { get; } = new();
    internal StringBuilder InputBuffer { get; } = new();
    internal TuiState State { get; } = new();
    internal TuiTransferSession TransferSession { get; } = new();

    internal IReadOnlyList<TuiCommandSuggestion> CommandSuggestions =>
        TuiCommands.GetSuggestions(InputBuffer.ToString(), State.AwaitingUserToken);

    internal int SelectedCommandSuggestionIndex =>
        GetSelectedCommandSuggestionIndex(CommandSuggestions);

    internal void RegisterCommands()
    {
        Register(_ => RequestQuit(), TuiCommands.Quit, TuiCommands.Exit, TuiCommands.QuitShort);
        Register(_ => ToggleHelp(), TuiCommands.Help, TuiCommands.HelpShort);
        Register(_ => Logs.Append(LogKind.Info, StatusSummary()), TuiCommands.Status);
        Register(HandleStorefrontCommand, TuiCommands.Storefront, TuiCommands.StorefrontShort);
        Register(HandleNameCommand, TuiCommands.Name);
        Register(HandleAuthCommand, TuiCommands.Auth);
        Register(_ => HandleConfigCommand(), TuiCommands.Config, TuiCommands.ConfigShort);
        Register(HandleAddCommand, TuiCommands.Add);
        Register(_ => HandleRunCommand(), TuiCommands.Run);
    }

    internal void ProcessMessage(TuiMessage msg)
    {
        if (
            msg is TransferMessage { TransferId: var transferId }
            && TransferSession.IsStale(transferId)
        )
        {
            return;
        }

        switch (msg)
        {
            case KeyPressedMsg m:
                HandleKey(m.Key);
                break;
            case QuitRequestedMsg:
                RequestQuit();
                break;
            case FatalErrorMsg m:
                HandleFatalError(m.Error);
                break;
            case AuthFailedMsg m:
                HandleAuthFailed(m.Error);
                break;
            case TransferFailedMsg m:
                HandleTransferFailed(m.Error);
                break;
            case AuthDoneMsg m:
                HandleAuthDone(m);
                break;
            case PlaylistFetchedMsg m:
                HandlePlaylistFetched(m);
                break;
            case IsrcProgressMsg m:
                State.ProgressCurrent = m.Current;
                State.ProgressTotal = m.Total;
                break;
            case IsrcDoneMsg m:
                HandleIsrcDone(m);
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
                HandleTextDone(m);
                break;
            case PlaylistCreatedMsg m:
                HandlePlaylistCreated(m);
                break;
        }
    }

    internal string StatusSummary()
    {
        var dev = tokenCache.HasValidDeveloperToken ? "valid" : "missing";
        var user = tokenCache.HasValidUserToken ? "valid" : "missing";
        return $"Developer token: {dev}  |  User token: {user}  |  Storefront: {State.Storefront}";
    }

    private void Register(Action<string?> handler, params string[] aliases)
    {
        foreach (var alias in aliases)
            _commands[alias] = handler;
    }

    private void HandleKey(ConsoleKeyInfo key)
    {
        if (HandleQuitShortcut(key))
            return;

        if (TryHandlePhaseInput(key) || State.Phase is not TuiTransferPhase.Idle)
            return;

        HandleGeneralInput(key);
    }

    private bool TryHandlePhaseInput(ConsoleKeyInfo key)
    {
        if (TryHandleTransferCancelInput(key))
            return true;

        return State is { ShowHelp: true, Phase: TuiTransferPhase.Idle }
            ? TryHandleHelpInput(key)
            : State.Phase switch
            {
                TuiTransferPhase.ConfirmPlaylist => TryHandlePlaylistConfirmInput(key),
                TuiTransferPhase.ConfirmTextMatch => HandleConfirmKey(key),
                TuiTransferPhase.Done => TryHandleDonePhaseInput(key),
                _ => false,
            };
    }

    private bool TryHandleTransferCancelInput(ConsoleKeyInfo key)
    {
        if (key.Key is not ConsoleKey.Escape)
            return false;

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

        CancelTransfer();
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

    private bool HandleQuitShortcut(ConsoleKeyInfo key)
    {
        if (key is not { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control })
            return false;

        RequestQuit();
        return true;
    }

    private bool TryHandlePlaylistConfirmInput(ConsoleKeyInfo key)
    {
        if (key.Key is ConsoleKey.Enter)
        {
            if (State.TransferTracks is null)
                return true;

            var transferId = TransferSession.Id;
            var tracks = State.TransferTracks.ToList();
            var storefront = State.Storefront;

            State.Phase = TuiTransferPhase.ResolvingIsrc;
            State.ProgressCurrent = 0;
            State.ProgressTotal = tracks.Count;
            Logs.Append(LogKind.Info, "Starting ISRC matching...");
            StartTransferStep(transferId, (id, ct) => matchByIsrc(id, tracks, storefront, ct));
        }
        else if (key.Key is ConsoleKey.Escape or ConsoleKey.Backspace)
        {
            CancelTransfer();
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
                if (State.AllResults is not null)
                    ScrollOffsetDown(State.AllResults.Count, getVisibleDoneRows());
                return true;
            case ConsoleKey.Enter:
                ResetTransferState();
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

    private void HandleGeneralInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                HandleEnter();
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
                    InputBuffer.Remove(InputBuffer.Length - 1, 1);
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

        if (
            GetSelectedCommandSuggestionIndex(suggestions) == 0
            && !IsSelectedCommandSuggestion(suggestions[0])
        )
        {
            _selectedCommandSuggestionCompletion = suggestions[0].Completion;
        }
    }

    private void CompleteCommandSuggestion()
    {
        var suggestions = CommandSuggestions;
        if (suggestions.Count == 0)
            return;

        var selectedSuggestion = suggestions[GetSelectedCommandSuggestionIndex(suggestions)];
        InputBuffer.Clear().Append(CompleteWithTrailingSpace(selectedSuggestion.Completion));
        _selectedCommandSuggestionCompletion = selectedSuggestion.Completion;
        ClampCommandSuggestionSelection();
    }

    private static string CompleteWithTrailingSpace(string completion) =>
        completion.EndsWith(' ') ? completion : completion + ' ';

    private int GetSelectedCommandSuggestionIndex(IReadOnlyList<TuiCommandSuggestion> suggestions)
    {
        if (suggestions.Count == 0)
            return 0;

        for (var i = 0; i < suggestions.Count; i++)
        {
            if (IsSelectedCommandSuggestion(suggestions[i]))
                return i;
        }

        return 0;
    }

    private bool IsSelectedCommandSuggestion(TuiCommandSuggestion suggestion) =>
        suggestion.Completion == _selectedCommandSuggestionCompletion;

    private void HandleEscape()
    {
        InputBuffer.Clear();
        _selectedCommandSuggestionCompletion = null;
        if (!State.AwaitingUserToken)
            return;

        State.AwaitingUserToken = false;
        Logs.Append(LogKind.Info, "Authentication cancelled.");
    }

    private bool HandleConfirmKey(ConsoleKeyInfo key)
    {
        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'y' or '\r' or '\n':
                if (State.UnmatchedTracks is null)
                    return true;

                var transferId = TransferSession.Id;
                var unmatchedTracks = State.UnmatchedTracks.ToList();
                var storefront = State.Storefront;

                State.Phase = TuiTransferPhase.TextMatching;
                State.ProgressCurrent = 0;
                State.ProgressTotal = unmatchedTracks.Count;
                State.ProgressLabel = string.Empty;
                Logs.Append(LogKind.Info, "Starting text matching...");
                StartTransferStep(
                    transferId,
                    (id, ct) => matchByText(id, unmatchedTracks, storefront, ct)
                );
                break;
            case 'n':
                Logs.Append(LogKind.Info, "Text matching skipped.");
                if (State.UnmatchedTracks is not null)
                {
                    State.TextResults ??= [];
                    foreach (var t in State.UnmatchedTracks)
                        State.TextResults.Add(new MatchResult.NotFound(t, "Skipped"));
                }

                State.Phase = TuiTransferPhase.CreatingPlaylist;
                StartCreatePlaylist(TransferSession.Id);
                break;
        }

        return true;
    }

    private void HandleEnter()
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
            return;

        if (raw.StartsWith('/'))
        {
            HandleCommand(raw);
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

            StartPlaylistFetch([urlInfo.Id], "Starting transfer...");
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

    private void HandleCommand(string raw)
    {
        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0];
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        if (_commands.TryGetValue(cmd, out var handler))
        {
            handler(arg);
            return;
        }

#pragma warning disable CA1308
        Logs.Append(LogKind.Error, $"Unknown command: {cmd.ToLowerInvariant()}. Type /help");
#pragma warning restore CA1308
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

    private void HandleAuthCommand(string? argument)
    {
        Logs.Clear();

        if ("reset".Equals(argument, StringComparison.OrdinalIgnoreCase))
        {
            tokenCache.Clear();
            Logs.Append(LogKind.Warning, "Tokens cleared.");
        }

        Logs.Append(LogKind.Info, "Authenticating...");
        startAuth();
    }

    private void HandleConfigCommand()
    {
        try
        {
            ConfigurationFolderOpener.Open();
            Logs.Append(
                LogKind.Success,
                $"Opened config folder: {ConfigurationFolderOpener.ConfigDirectory}"
            );
        }
        catch (Exception e) when (ConfigurationFolderOpener.IsOpenFailure(e))
        {
            Logs.Append(LogKind.Error, $"Could not open config folder: {e.Message}");
        }
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

    private void HandleRunCommand()
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
            $"Starting transfer of {playlistIdsToFetch.Count} merged playlists..."
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

    private void HandleIsrcDone(IsrcDoneMsg msg)
    {
        State.IsrcResults = msg.Matched;
        State.UnmatchedTracks = msg.Unmatched;

        Logs.Append(
            LogKind.Success,
            $"{msg.Matched.Count}/{State.TransferTracks?.Count ?? 0} matched via ISRC"
        );
        if (msg.Unmatched.Count > 0)
            Logs.Append(LogKind.Info, $"{msg.Unmatched.Count} remaining");

        Logs.Append(LogKind.Separator, string.Empty);

        if (msg.Unmatched.Count > 0)
        {
            State.Phase = TuiTransferPhase.ConfirmTextMatch;
        }
        else
        {
            State.Phase = TuiTransferPhase.CreatingPlaylist;
            StartCreatePlaylist(msg.TransferId);
        }
    }

    private void HandleTextDone(TextDoneMsg msg)
    {
        State.TextResults = msg.Results;

        var textMatched = msg.Results.Count(r => r is MatchResult.Matched);
        Logs.Append(
            LogKind.Success,
            $"{textMatched}/{State.UnmatchedTracks?.Count ?? 0} matched via text"
        );
        Logs.Append(LogKind.Separator, string.Empty);

        State.Phase = TuiTransferPhase.CreatingPlaylist;
        StartCreatePlaylist(msg.TransferId);
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

    private void HandleTransferFailed(Exception err)
    {
        TransferSession.CancelAndInvalidate();
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

    private void HandleFatalError(Exception err)
    {
        TransferSession.CancelCurrent();
        State.Phase = TuiTransferPhase.Idle;
        State.AwaitingUserToken = false;
        Logs.Append(LogKind.Error, $"Fatal background error: {err.Message}");
    }

    private void RequestQuit()
    {
        State.QuitRequested = true;
        TransferSession.CancelCurrent();
        cancelApp();
    }

    private void CancelTransfer()
    {
        TransferSession.CancelAndInvalidate();
        ResetTransferState();
        Logs.Append(LogKind.Info, "Transfer cancelled.");
    }

    private void StartPlaylistFetch(IReadOnlyCollection<string> playlistIds, string startMessage)
    {
        var transferId = TransferSession.Begin();
        ResetTransferState();
        State.Phase = TuiTransferPhase.FetchingPlaylist;
        Logs.Clear();
        Logs.Append(LogKind.Info, startMessage);
        StartTransferStep(transferId, (id, ct) => fetchPlaylists(id, playlistIds, ct));
    }

    private void StartCreatePlaylist(int transferId)
    {
        var allResults = BuildAllResults();
        var playlistName = State.PlaylistName;
        StartTransferStep(transferId, (id, ct) => createPlaylist(id, playlistName, allResults, ct));
    }

    private void StartTransferStep(int transferId, Func<int, CancellationToken, Task> operation) =>
        TransferSession.Start(transferId, operation, messages, appToken);

    private List<MatchResult> BuildAllResults()
    {
        var allResults = new List<MatchResult>();
        var isrcMap = (State.IsrcResults ?? [])
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);
        var textMap = (State.TextResults ?? [])
            .DistinctBy(m => m.SpotifyTrack.SpotifyId)
            .ToDictionary(m => m.SpotifyTrack.SpotifyId);

        foreach (var track in State.TransferTracks ?? [])
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

    private void ResetTransferState() => State.ResetTransferState();

    public void Dispose() => TransferSession.Dispose();
}
