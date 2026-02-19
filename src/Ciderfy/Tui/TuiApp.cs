using System.Text;
using System.Threading.Channels;
using Ciderfy.Apple;
using Ciderfy.Matching;
using Spectre.Console;

namespace Ciderfy.Tui;

/// <summary>
/// Full-screen alternate-screen TUI that drives the Spotify to Apple Music transfer pipeline
/// </summary>
internal sealed partial class TuiApp(
    TokenCache tokenCache,
    AppleMusicAuth auth,
    PlaylistTransferService transferService
) : IDisposable
{
    private readonly Channel<TuiMessage> _channel = Channel.CreateUnbounded<TuiMessage>();
    private readonly CancellationTokenSource _cts = new();
    private readonly LogBuffer _logs = new();
    private readonly StringBuilder _inputBuffer = new();
    private readonly TuiCommandRegistry _commands = new();
    private bool _commandsRegistered;

    private TuiTransferPhase _phase = TuiTransferPhase.Idle;
    private string _storefront = "us";
    private string? _nextPlaylistName;
    private bool _awaitingUserToken;
    private bool _showHelp;
    private bool _quit;

    // Queue data
    private readonly List<string> _queuedPlaylistUrls = [];

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
        EnsureCommandsRegistered();
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

    private async Task RunFetchPlaylistAsync(List<string> playlistIds, CancellationToken ct)
    {
        try
        {
            if (!tokenCache.HasValidDeveloperToken)
                await auth.GetDeveloperTokenAsync(ct);

            if (!tokenCache.HasValidUserToken)
            {
                _channel.Writer.TryWrite(
                    new PlaylistFetchedMsg(
                        [],
                        new InvalidOperationException("User token missing. Run /auth first.")
                    )
                );
                return;
            }

            var fetchTasks = playlistIds.Select(id =>
                transferService.FetchSpotifyPlaylistAsync(id, ct)
            );
            var playlists = await Task.WhenAll(fetchTasks);
            _channel.Writer.TryWrite(new PlaylistFetchedMsg([.. playlists], null));
        }
        catch (Exception ex)
        {
            _channel.Writer.TryWrite(new PlaylistFetchedMsg([], ex));
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

    // Message handling lives in TuiApp.Messages.cs

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
