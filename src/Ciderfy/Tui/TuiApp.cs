using System.Text;
using System.Threading.Channels;
using Ciderfy.Apple;
using Ciderfy.Matching;
using Ciderfy.Spotify;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Ciderfy.Tui;

/// <summary>
/// Tracks which stage of the playlist transfer pipeline the TUI is in
/// </summary>
internal enum TuiTransferPhase
{
    Idle,
    FetchingPlaylist,
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

/// <summary>
/// Full-screen alternate-screen TUI that drives the Spotify to Apple Music transfer pipeline
/// </summary>
/// <remarks>
/// Uses <see cref="Spectre.Console.LiveDisplay"/> with a message-passing architecture:
/// background tasks post <see cref="TuiMessage"/> records to a channel, and the render
/// loop processes them on the main thread.
/// </remarks>
internal sealed class TuiApp(
    TokenCache tokenCache,
    AppleMusicAuth auth,
    PlaylistTransferService transferService
) : IDisposable
{
    private readonly Channel<TuiMessage> _channel = Channel.CreateUnbounded<TuiMessage>();
    private readonly CancellationTokenSource _cts = new();
    private readonly LogBuffer _logs = new();
    private readonly StringBuilder _inputBuffer = new();

    private TuiTransferPhase _phase = TuiTransferPhase.Idle;
    private string _storefront = "us";
    private string? _nextPlaylistName;
    private bool _awaitingUserToken;
    private bool _showHelp;
    private bool _quit;

    // Spinner and progress
    private int _spinnerTick;
    private int _progressCurrent;
    private int _progressTotal;
    private string _progressLabel = "";

    // Transfer data
    private List<TrackMetadata>? _transferTracks;
    private List<MatchResult.Matched>? _isrcResults;
    private List<TrackMetadata>? _unmatchedTracks;
    private List<MatchResult>? _textResults;
    private string _playlistName = "";

    // Results scroll
    private List<MatchResult>? _allResults;
    private int _scrollOffset;

    // Cursor blink
    private bool _cursorVisible = true;

    /// <summary>
    /// Enters the alternate screen and runs the TUI event loop until the user quits
    /// </summary>
    public Task<int> RunAsync()
    {
        _logs.Append(LogKind.Info, "Paste a Spotify playlist URL to transfer, or type /help");

        _ = RunTickTimerAsync(_cts.Token);

        RunUiLoop();
        return Task.FromResult(0);
    }

    private void RunUiLoop()
    {
        AnsiConsole.Console.AlternateScreen(() =>
        {
            Console.CursorVisible = false;
            try
            {
                RunLiveDisplay();
            }
            finally
            {
                Console.CursorVisible = true;
            }
        });
    }

    private void RunLiveDisplay()
    {
        AnsiConsole
            .Live(new Text("Loading..."))
            .Overflow(VerticalOverflow.Crop)
            .Cropping(VerticalOverflowCropping.Bottom)
            .AutoClear(true)
            .StartAsync(RunRenderLoopAsync)
            .GetAwaiter()
            .GetResult();
    }

    private async Task RunRenderLoopAsync(LiveDisplayContext ctx)
    {
        _ = Task.Run(() => ReadKeysLoop(_cts.Token), _cts.Token);

        while (ShouldContinueRunning())
        {
            DrainPendingMessages();
            RenderFrame(ctx);
            await WaitForNextMessageOrTimeoutAsync().ConfigureAwait(false);
        }

        RenderFinalFrame(ctx);
    }

    private bool ShouldContinueRunning() => !_quit && !_cts.IsCancellationRequested;

    private void DrainPendingMessages()
    {
        while (_channel.Reader.TryRead(out var msg))
        {
            ProcessMessage(msg);
        }
    }

    private void RenderFrame(LiveDisplayContext ctx)
    {
        ctx.UpdateTarget(BuildView());
        ctx.Refresh();
    }

    private static void RenderFinalFrame(LiveDisplayContext ctx)
    {
        ctx.UpdateTarget(new Text(""));
        ctx.Refresh();
    }

    private async Task WaitForNextMessageOrTimeoutAsync()
    {
        try
        {
            using var delayCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token);
            delayCts.CancelAfter(50);
            await _channel.Reader.WaitToReadAsync(delayCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Either timeout or quit
        }
    }

    private async Task RunTickTimerAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(80));
        while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
        {
            _channel.Writer.TryWrite(new TickMsg());
        }
    }

    private void ReadKeysLoop(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            if (!TryReadKey(out var key))
                continue;

            if (HandleQuitShortcut(key))
                return;

            if (TryHandleConfirmPhaseInput(key))
                continue;

            if (TryHandleDonePhaseInput(key))
                continue;

            if (!CanAcceptGeneralInput())
                continue;

            HandleGeneralInput(key);
        }
    }

    private static bool TryReadKey(out ConsoleKeyInfo key)
    {
        if (!Console.KeyAvailable)
        {
            Thread.Sleep(20);
            key = default;
            return false;
        }

        key = Console.ReadKey(true);
        return true;
    }

    private bool HandleQuitShortcut(ConsoleKeyInfo key)
    {
        if (key is not { Key: ConsoleKey.C, Modifiers: ConsoleModifiers.Control })
            return false;

        _quit = true;
        _cts.Cancel();
        return true;
    }

    private bool TryHandleConfirmPhaseInput(ConsoleKeyInfo key)
    {
        if (_phase is not TuiTransferPhase.ConfirmTextMatch)
            return false;

        HandleConfirmKey(key);
        return true;
    }

    private bool TryHandleDonePhaseInput(ConsoleKeyInfo key)
    {
        if (_phase is not TuiTransferPhase.Done)
            return false;

        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
                ScrollResultsUp();
                return true;
            case ConsoleKey.DownArrow:
                ScrollResultsDown();
                return true;
            case ConsoleKey.Enter:
                ResetTransferState();
                return true;
            default:
                return false;
        }
    }

    private void ScrollResultsUp() => _scrollOffset = Math.Max(0, _scrollOffset - 1);

    private void ScrollResultsDown()
    {
        if (_allResults is null)
            return;

        var visibleRows = Math.Max(
            3,
            Console.WindowHeight - FixedChromeHeight - DoneViewChromeHeight
        );
        _scrollOffset = Math.Min(Math.Max(0, _allResults.Count - visibleRows), _scrollOffset + 1);
    }

    private bool CanAcceptGeneralInput() =>
        _phase is TuiTransferPhase.Idle or TuiTransferPhase.Done;

    private void HandleGeneralInput(ConsoleKeyInfo key)
    {
        switch (key.Key)
        {
            case ConsoleKey.Enter:
                HandleEnter();
                break;
            case ConsoleKey.Backspace:
                RemoveLastInputCharacter();
                break;
            case ConsoleKey.Escape:
                _inputBuffer.Clear();
                break;
            default:
                AppendInputCharacter(key.KeyChar);
                break;
        }
    }

    private void RemoveLastInputCharacter()
    {
        if (_inputBuffer.Length > 0)
            _inputBuffer.Remove(_inputBuffer.Length - 1, 1);
    }

    private void AppendInputCharacter(char keyChar)
    {
        if (keyChar >= 32)
            _inputBuffer.Append(keyChar);
    }

    private void HandleConfirmKey(ConsoleKeyInfo key)
    {
        switch (char.ToLowerInvariant(key.KeyChar))
        {
            case 'y' or '\r' or '\n':
                _phase = TuiTransferPhase.TextMatching;
                _progressCurrent = 0;
                _progressTotal = _unmatchedTracks?.Count ?? 0;
                _progressLabel = "";
                _logs.Append(LogKind.Info, "Starting text matching...");
                _ = Task.Run(() => RunTextMatchAsync(_cts.Token));
                break;
            case 'n':
                _logs.Append(LogKind.Info, "Text matching skipped.");
                if (_unmatchedTracks is not null)
                {
                    _textResults ??= [];
                    foreach (var t in _unmatchedTracks)
                        _textResults.Add(new MatchResult.NotFound(t, "Skipped"));
                }

                _phase = TuiTransferPhase.CreatingPlaylist;
                _ = Task.Run(() => RunCreatePlaylistAsync(_cts.Token));
                break;
        }
    }

    private void HandleEnter()
    {
        if (_phase is TuiTransferPhase.Done)
        {
            ResetTransferState();
            return;
        }

        _showHelp = false;

        var raw = _inputBuffer.ToString().Trim();
        _inputBuffer.Clear();

        if (string.IsNullOrEmpty(raw))
            return;

        if (_awaitingUserToken)
        {
            HandleUserTokenInput(raw);
            return;
        }

        if (raw.StartsWith('/'))
        {
            HandleCommand(raw);
            return;
        }

        if (SpotifyUrlInfo.TryParse(raw, out var urlInfo) && urlInfo is not null)
        {
            if (urlInfo.Type != SpotifyUrlType.Playlist)
            {
                _logs.Append(
                    LogKind.Error,
                    "Only playlist URLs are supported. Paste a Spotify playlist URL."
                );
            }
            else
            {
                ResetTransferState();
                _phase = TuiTransferPhase.FetchingPlaylist;
                _logs.Append(LogKind.Info, "Starting transfer...");
                _ = Task.Run(() => RunFetchPlaylistAsync(urlInfo.Id, _cts.Token));
            }

            return;
        }

        _logs.Append(LogKind.Error, "Not a valid command or Spotify URL. Type /help");
    }

    private void HandleUserTokenInput(string raw)
    {
        var trimmed = raw.Trim().Trim('"', '\'');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            _logs.Append(LogKind.Error, "No token provided.");
            return;
        }

        tokenCache.UserToken = trimmed;
        tokenCache.UserTokenExpiry = DateTimeOffset.UtcNow.AddMonths(3);
        tokenCache.Save();

        _awaitingUserToken = false;
        _logs.Append(LogKind.Success, "Authentication complete! Tokens cached.");
        _logs.Append(LogKind.Info, StatusSummary());
    }

    private void HandleCommand(string raw)
    {
        var parts = raw.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var cmd = parts[0].ToLowerInvariant();
        var arg = parts.Length > 1 ? parts[1].Trim() : null;

        switch (cmd)
        {
            case "/quit" or "/exit" or "/q":
                _quit = true;
                _cts.Cancel();
                break;

            case "/help"
            or "/h":
                _showHelp = !_showHelp;
                break;

            case "/status":
                _logs.Append(LogKind.Info, StatusSummary());
                break;

            case "/storefront"
            or "/sf":
                if (string.IsNullOrEmpty(arg))
                {
                    _logs.Append(LogKind.Info, $"Storefront: {_storefront}. Usage: /storefront fr");
                }
                else
                {
                    _storefront = arg.ToLowerInvariant();
                    _logs.Append(LogKind.Success, $"Storefront set to {_storefront}");
                }

                break;

            case "/name":
                if (string.IsNullOrEmpty(arg))
                {
                    _nextPlaylistName = null;
                    _logs.Append(LogKind.Success, "Playlist name override cleared");
                }
                else
                {
                    _nextPlaylistName = arg;
                    _logs.Append(
                        LogKind.Success,
                        $"Next playlist name set to \"{_nextPlaylistName}\""
                    );
                }

                break;

            case "/auth":
                if ("reset".Equals(arg, StringComparison.OrdinalIgnoreCase))
                {
                    tokenCache.Clear();
                    _logs.Append(LogKind.Warning, "Tokens cleared.");
                }

                _logs.Append(LogKind.Info, "Authenticating...");
                _ = Task.Run(() => RunAuthAsync(_cts.Token));
                break;

            default:
                _logs.Append(LogKind.Error, $"Unknown command: {cmd}. Type /help");
                break;
        }
    }

    // Background operations
    private async Task RunAuthAsync(CancellationToken ct)
    {
        try
        {
            await auth.GetDeveloperTokenAsync(ct);
            var needsUserToken = !tokenCache.HasValidUserToken;
            _channel.Writer.TryWrite(new AuthDoneMsg(needsUserToken, null));
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(new AuthDoneMsg(false, ex));
        }
    }

    private async Task RunFetchPlaylistAsync(string playlistId, CancellationToken ct)
    {
        try
        {
            if (!tokenCache.HasValidDeveloperToken)
                await auth.GetDeveloperTokenAsync(ct);

            if (!tokenCache.HasValidUserToken)
            {
                _channel.Writer.TryWrite(
                    new PlaylistFetchedMsg(
                        null,
                        new InvalidOperationException("User token missing. Run /auth first.")
                    )
                );
                return;
            }

            var playlist = await transferService.FetchSpotifyPlaylistAsync(playlistId, ct);
            _channel.Writer.TryWrite(new PlaylistFetchedMsg(playlist, null));
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(new PlaylistFetchedMsg(null, ex));
        }
    }

    private async Task RunIsrcMatchAsync(CancellationToken ct)
    {
        try
        {
            var progress = new Progress<(int Current, int Total)>(p =>
                _channel.Writer.TryWrite(new IsrcProgressMsg(p.Current, p.Total))
            );

            var (matched, unmatched) = await transferService.MatchByIsrcAsync(
                _transferTracks!,
                _storefront,
                progress,
                ct
            );
            _channel.Writer.TryWrite(new IsrcDoneMsg(matched, unmatched, null));
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(new IsrcDoneMsg([], [], ex));
        }
    }

    private async Task RunTextMatchAsync(CancellationToken ct)
    {
        try
        {
            var progress = new Progress<TrackMatchProgress>(p =>
                _channel.Writer.TryWrite(
                    new TextProgressMsg(p.Track, p.CurrentIndex, _unmatchedTracks!.Count)
                )
            );

            var results = await transferService.MatchByTextAsync(
                _unmatchedTracks!,
                _storefront,
                progress,
                ct
            );
            _channel.Writer.TryWrite(new TextDoneMsg(results, null));
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(new TextDoneMsg(null, ex));
        }
    }

    private async Task RunCreatePlaylistAsync(CancellationToken ct)
    {
        try
        {
            var allResults = BuildAllResults();
            var result = await transferService.CreatePlaylistAsync(_playlistName, allResults, ct);
            _channel.Writer.TryWrite(new PlaylistCreatedMsg(result, null));
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(new PlaylistCreatedMsg(null, ex));
        }
    }

    // Message processing
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

        _phase = TuiTransferPhase.ResolvingIsrc;
        _progressCurrent = 0;
        _progressTotal = _transferTracks.Count;
        _ = Task.Run(() => RunIsrcMatchAsync(_cts.Token));
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

    // Lines consumed by fixed UI elements (everything except the content area).
    // Panel border (2) + Banner(6) + Rule (1) + Badges (1) + Gaps(2) + Input panel (3) + Footer (1)
    private const int FixedChromeHeight = 2 + 6 + 1 + 1 + 2 + 3 + 1;

    // Lines consumed within the Done view by non-table elements.
    // Summary panel (3) + Gap(1) + Table header and borders (3) + Gap (1) + Hint (1)
    private const int DoneViewChromeHeight = 3 + 1 + 3 + 1 + 1;

    private Panel BuildView()
    {
        var width = Math.Max(24, Console.WindowWidth - 2);
        var height = Console.WindowHeight;
        var contentWidth = Math.Max(20, width - 4);
        var contentHeight = Math.Max(4, height - FixedChromeHeight);

        // Banner + separator + badges
        var banner = Components.RenderBanner();
        var separator = new Rule { Style = new Style(Theme.GrayColor) };
        var badges = Components.RenderStatusBadges(
            tokenCache.HasValidDeveloperToken,
            tokenCache.HasValidUserToken,
            _storefront,
            _nextPlaylistName
        );

        // Main content area
        var content =
            _phase is TuiTransferPhase.Done
                ? RenderDoneView(contentHeight)
                : RenderActiveView(contentWidth, contentHeight);

        // Input
        IRenderable input;
        if (_awaitingUserToken)
            input = Components.RenderInputWithPrompt(
                "token>",
                _inputBuffer.ToString(),
                _cursorVisible,
                contentWidth
            );
        else
            input = Components.RenderInput(_inputBuffer.ToString(), _cursorVisible, contentWidth);

        var footer = Components.RenderFooter();

        var rows = new Rows(
            banner,
            separator,
            badges,
            new Text(""),
            content,
            new Text(""),
            input,
            footer
        );

        return new Panel(rows)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Theme.GrayColor),
            Padding = new Padding(1, 0),
            Width = width,
            Height = height,
        };
    }

    private IRenderable RenderActiveView(int width, int contentHeight)
    {
        // Reserve lines for progress section if active
        var progressLines = _phase switch
        {
            TuiTransferPhase.FetchingPlaylist => 1,
            TuiTransferPhase.ResolvingIsrc => 2,
            TuiTransferPhase.ConfirmTextMatch => 1,
            TuiTransferPhase.TextMatching => 2,
            TuiTransferPhase.CreatingPlaylist => 1,
            _ => 0,
        };

        // Subtract progress section and empty line separator
        var logHeight =
            progressLines > 0 ? Math.Max(4, contentHeight - progressLines - 1) : contentHeight;

        var progressSection = _phase switch
        {
            TuiTransferPhase.FetchingPlaylist => Components.RenderSpinnerLine(
                "Fetching Spotify playlist...",
                _spinnerTick
            ),
            TuiTransferPhase.ResolvingIsrc => Components.RenderProgressBar(
                $"Resolving ISRCs via Deezer ({_progressCurrent}/{_progressTotal})",
                _progressTotal > 0 ? (double)_progressCurrent / _progressTotal : 0,
                width
            ),
            TuiTransferPhase.ConfirmTextMatch => Components.RenderConfirmPrompt(
                _unmatchedTracks?.Count ?? 0
            ),
            TuiTransferPhase.TextMatching => Components.RenderProgressBar(
                string.IsNullOrEmpty(_progressLabel)
                    ? "Text matching..."
                    : $"Matching: {_progressLabel}",
                _progressTotal > 0 ? (double)_progressCurrent / _progressTotal : 0,
                width
            ),
            TuiTransferPhase.CreatingPlaylist => Components.RenderSpinnerLine(
                "Creating Apple Music playlist...",
                _spinnerTick
            ),
            _ => null,
        };

        if (_showHelp && _phase is TuiTransferPhase.Idle)
        {
            var helpHeight = 12;
            var helpLogHeight = Math.Max(2, contentHeight - helpHeight - 2);
            var logArea = Components.RenderLogArea(_logs, width, helpLogHeight);
            return new Rows(
                logArea,
                new Text(""),
                Components.RenderHelpTable(),
                new Markup($"[{Theme.Muted}]Press Enter to dismiss[/]")
            );
        }

        var logs = Components.RenderLogArea(_logs, width, logHeight);

        if (progressSection is not null)
            return new Rows(logs, new Text(""), progressSection);

        return logs;
    }

    private IRenderable RenderDoneView(int contentHeight)
    {
        if (_allResults is null || _transferTracks is null)
            return new Text("No results");

        var matched = _allResults.OfType<MatchResult.Matched>().Count();
        var total = _transferTracks.Count;
        var notFound = total - matched;

        var summary = Components.RenderSummaryPanel(_playlistName, matched, total, notFound);
        var visibleRows = Math.Max(3, contentHeight - DoneViewChromeHeight);
        var table = Components.RenderResultsTable(_allResults, _scrollOffset, visibleRows);
        var hint = Components.RenderScrollHint(_scrollOffset, _allResults.Count, visibleRows);

        return new Rows(summary, new Text(""), table, new Text(""), hint);
    }

    private string StatusSummary()
    {
        var dev = tokenCache.HasValidDeveloperToken ? "valid" : "missing";
        var user = tokenCache.HasValidUserToken ? "valid" : "missing";
        return $"Developer token: {dev}  |  User token: {user}  |  Storefront: {_storefront}";
    }

    private void ResetTransferState()
    {
        _phase = TuiTransferPhase.Idle;
        _progressCurrent = 0;
        _progressTotal = 0;
        _progressLabel = "";
        _transferTracks = null;
        _isrcResults = null;
        _unmatchedTracks = null;
        _textResults = null;
        _allResults = null;
        _playlistName = "";
        _scrollOffset = 0;
    }

    public void Dispose() => _cts.Dispose();
}
